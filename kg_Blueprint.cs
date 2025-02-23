using System.Diagnostics;
using System.Threading;
using ItemManager;
using LocalizationManager;
using PieceManager;

namespace kg_Blueprint;

[BepInPlugin(GUID, NAME, VERSION)]
public class kg_Blueprint : BaseUnityPlugin
{
    public static kg_Blueprint _thistype;
    private const string GUID = "kg.Blueprint";
    private const string NAME = "Blueprint";
    private const string VERSION = "1.0.0";
    public new static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(GUID);
    public static readonly AssetBundle Asset = GetAssetBundle("kg_blueprint");
    public static readonly string BlueprintsPath = Path.Combine(Paths.ConfigPath, "Blueprints");
    private static readonly List<GameObject> ReplaceMaterials = [];
    private void Awake() 
    {
        _thistype = this;
        Localizer.Load();
        LoadAsm("kg_BlueprintScripts");
        if (!Directory.Exists(BlueprintsPath)) Directory.CreateDirectory(BlueprintsPath); 
        ReplaceMaterials.Add(new BuildPiece(Asset, "kg_BlueprintBox").Prefab.AddComponent<BlueprintPiece>().gameObject);
        ReplaceMaterials.Add(new BuildPiece(Asset, "kg_BlueprintBox_Large").Prefab.AddComponent<BlueprintPiece>().gameObject);
        new Item(Asset, "kg_BlueprintHammer"){ Configurable = Configurability.Recipe };
        Configs.Init(); 
        BlueprintUI.Init();
        BuildProgress.Init();
        ReadBlueprints();
        new Harmony(GUID).PatchAll();
    }
    private void FixedUpdate() => PlayerState.Update();
    private void Update()
    {
        BlueprintUI.Update();
        InteractionUI.Update();
    }
    private static AssetBundle GetAssetBundle(string filename)
    {
        Assembly execAssembly = Assembly.GetExecutingAssembly(); 
        string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(filename));
        using Stream stream = execAssembly.GetManifestResourceStream(resourceName)!;
        return AssetBundle.LoadFromStream(stream);
    } 
    private static CancellationTokenSource _cts = new CancellationTokenSource();
    public static void ReadBlueprints()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;
        Task.Run(() =>
        {
            string lastFile = "";
            try
            {
                token.ThrowIfCancellationRequested();
                Stopwatch stopwatch = Stopwatch.StartNew();
                List<BlueprintRoot> Blueprints = []; 
                Deserializer deserializer = new Deserializer(); 
                string[] files = Directory.GetFiles(BlueprintsPath, "*.yml", SearchOption.AllDirectories);
                if (files.Length == 0) return;
                for (int i = 0; i < files.Length; ++i)
                {
                    lastFile = files[i];
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        BlueprintRoot root = deserializer.Deserialize<BlueprintRoot>(File.ReadAllText(files[i]));
                        if (!root.IsValid(out string reason))
                        {
                            Logger.LogError($"Blueprint {files[i]} is invalid: {reason}");
                            continue;
                        }
                        root.AssignPath(files[i], true);
                        Blueprints.Add(root);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Error reading blueprint {files[i]}: {e}");
                    }
                }
                kg_Blueprint.Logger.LogDebug($"Loaded {Blueprints.Count} blueprints in {stopwatch.ElapsedMilliseconds}ms");
                token.ThrowIfCancellationRequested();
                ThreadingHelper.Instance.StartSyncInvoke(() => BlueprintUI.Load(Blueprints));
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) Logger.LogDebug("Blueprint loading canceled.");
                else Logger.LogError($"Error loading blueprints [{lastFile}]: {ex}");
            }
        }, token);
    }
    private static void LoadAsm(string name)
    {
        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("kg_Blueprint.Assets." + name + ".dll")!;
        byte[] buffer = new byte[stream.Length];
        stream.Read(buffer, 0, buffer.Length); 
        try 
        {
            Assembly.Load(buffer);
            stream.Dispose();
        }
        catch(Exception ex)
        {
            Logger.LogError($"Error loading {name} assembly\n:{ex}");
        } 
    }
    [HarmonyPatch(typeof(ZNetScene),nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    { 
        [UsedImplicitly] private static void Postfix(ZNetScene __instance) 
        {
            foreach (GameObject obj in ReplaceMaterials) 
            { 
                Transform View = obj.transform.Find("Scale/View");
                MeshRenderer[] renderers = View.GetComponentsInChildren<MeshRenderer>(true);
                var orig = __instance.GetPrefab("Piece_grausten_floor_2x2").transform.Find("new/high").GetComponent<MeshRenderer>().material;
                foreach (MeshRenderer renderer in renderers) renderer.material = orig;
            }
        }
    }
}