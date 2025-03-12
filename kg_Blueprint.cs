using System.Diagnostics;
using System.Text;
using System.Threading;
using ItemManager;
using KeyManager;
using LocalizationManager;
using PieceManager;
using ServerSync;
using CraftingTable = ItemManager.CraftingTable;

namespace kg_Blueprint;

[BepInPlugin(GUID, NAME, VERSION)]
[VerifyKey("KGvalheim/BlueprintTest", LicenseMode.Always)]
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
    private static readonly List<GameObject> ReplaceShaders = []; 
    private static readonly ConfigSync configSync = new ServerSync.ConfigSync(GUID) { DisplayName = NAME, CurrentVersion = VERSION, MinimumRequiredVersion = VERSION, ModRequired = false, IsLocked = true};
    private static ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
    {
        ConfigEntry<T> configEntry = _thistype.Config.Bind(group, name, value, description);
        SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry); 
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;
        return configEntry;
    }
    
    public static ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    private void Awake()  
    {
        _thistype = this; 
        Localizer.Load();
        LoadAsm("kg_BlueprintScripts");
        PlanbuildParser.CreateIcon();
        BuildPiece blueprintBox = new BuildPiece(Asset, "kg_BlueprintBox");
        blueprintBox.RequiredItems.Add("Grausten", 20, false); 
        blueprintBox.RequiredItems.Add("SurtlingCore", 8, false); 
        BuildPiece blueprintBoxNoFloor = new BuildPiece(Asset, "kg_BlueprintBox_NoFloor");
        blueprintBoxNoFloor.RequiredItems.Add("Grausten", 20, false);
        blueprintBoxNoFloor.RequiredItems.Add("SurtlingCore", 8, false);
        blueprintBox.Prefab.AddComponent<BlueprintPiece>();
        blueprintBoxNoFloor.Prefab.AddComponent<BlueprintPiece>();
        ReplaceMaterials.Add(blueprintBox.Prefab);
        ReplaceMaterials.Add(blueprintBoxNoFloor.Prefab);
        BuildPiece sharingPiece = new BuildPiece(Asset, "kg_BlueprintSharing");
        sharingPiece.Prefab.AddComponent<BlueprintSharing>();
        sharingPiece.RequiredItems.Add("Wood", 30, true); 
        sharingPiece.RequiredItems.Add("Iron", 5, true); 
        ReplaceShaders.Add(sharingPiece.Prefab);
        Item blueprintHammer = new Item(Asset, "kg_BlueprintHammer"){ Configurable = Configurability.Recipe };
        blueprintHammer.RequiredItems.Add("Wood", 10);
        blueprintHammer.RequiredItems.Add("Blueberries", 5);
        blueprintHammer.Crafting.Add(CraftingTable.Inventory, 1);
        Configs.Init(); 
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        if (!Directory.Exists(BlueprintsPath)) Directory.CreateDirectory(BlueprintsPath); 
        BlueprintUI.Init(); 
        BuildProgress.Init();
        ReadBlueprints(); 
        new Harmony(GUID).PatchAll();
    }
    private void FixedUpdate() => PlayerState.Update();
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I)) kg_Blueprint.Logger.LogDebug($"Pieces: {Piece.s_allPieces.Count}");
        if (Input.GetKeyDown(KeyCode.Equals))
        {
            foreach (Piece piece in Piece.s_allPieces) piece.m_nview.Destroy();
            kg_Blueprint.Logger.LogDebug("Destroyed all pieces. Now: " + Piece.s_allPieces.Count);
        }
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
                IDeserializer builder = new DeserializerBuilder().WithTypeConverter(new IntOrStringConverter()).Build();
                Stopwatch stopwatch = Stopwatch.StartNew();
                List<BlueprintRoot> Blueprints = []; 
                string[] files = Directory.GetFiles(BlueprintsPath, "*.yml", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; ++i)
                { 
                    lastFile = files[i];
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        BlueprintRoot root = builder.Deserialize<BlueprintRoot>(File.ReadAllText(files[i]));
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
                
                string[] bp_files = Directory.GetFiles(BlueprintsPath, "*.blueprint", SearchOption.AllDirectories);
                for (int i = 0; i < bp_files.Length; ++i)
                {
                    lastFile = bp_files[i];
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        BlueprintRoot root = PlanbuildParser.Parse(File.ReadAllLines(bp_files[i]));
                        if (!root.IsValid(out string reason))
                        {
                            Logger.LogError($"PB Blueprint {bp_files[i]} is invalid: {reason}");
                            continue; 
                        }
                        root.AssignPath(bp_files[i], true);
                        root.Source = BlueprintRoot.SourceType.Planbuild;
                        Blueprints.Add(root); 
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Error reading PB blueprint {bp_files[i]}: {e}");
                    }
                }
                
                string[] vbuild_files = Directory.GetFiles(BlueprintsPath, "*.vbuild", SearchOption.AllDirectories);
                for (int i = 0; i < vbuild_files.Length; ++i)
                {
                    lastFile = vbuild_files[i];
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        BlueprintRoot root = VBuildParser.Parse(Path.GetFileNameWithoutExtension(vbuild_files[i]), File.ReadAllLines(vbuild_files[i]));
                        if (!root.IsValid(out string reason))
                        {
                            Logger.LogError($"VBuild Blueprint {vbuild_files[i]} is invalid: {reason}");
                            continue; 
                        }
                        root.AssignPath(vbuild_files[i], true);
                        root.Source = BlueprintRoot.SourceType.VBuild;
                        Blueprints.Add(root); 
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Error reading VBuild blueprint {vbuild_files[i]}: {e}");
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
    public static void TryLoadFromClipboard()
    {
        string clipboard = ClipboardUtils.GetText();
        if (string.IsNullOrWhiteSpace(clipboard)) return;
        Task.Run(() =>
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(clipboard); }
            catch (Exception) { bytes = null; }
            try
            { 
                string data = bytes == null ? clipboard : Encoding.UTF8.GetString(bytes);
                BlueprintRoot root = new DeserializerBuilder().WithTypeConverter(new IntOrStringConverter()).Build().Deserialize<BlueprintRoot>(data);
                if (!root.IsValid(out string reason))
                {
                    Logger.LogError($"Blueprint from clipboard is invalid: {reason}");
                    return;
                }
                root.AssignPath(Path.Combine(kg_Blueprint.BlueprintsPath, root.Name + ".yml"), false);
                root.Save(false);
                ThreadingHelper.Instance.StartSyncInvoke(() => BlueprintUI.AddEntry(root, true, true));
            }
            catch (Exception e)
            {
                Logger.LogError($"Error loading blueprint from clipboard: {e}");
            }
        });
    }
    public static void PasteBlueprintIntoClipboard(BlueprintRoot root)
    {
        if (root == null || !root.TryGetFilePath(out string path)) return;
        Task.Run(() =>
        {
            try
            {
                if (!File.Exists(path)) return;
                string data = Convert.ToBase64String(File.ReadAllBytes(path));
                ThreadingHelper.Instance.StartSyncInvoke(() => GUIUtility.systemCopyBuffer = data);
            }
            catch(Exception e)
            {
                Logger.LogError($"Error pasting blueprint into clipboard: {e}");
            }
        });
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
            Material orig = __instance.GetPrefab("Piece_grausten_floor_2x2").transform.Find("new/high").GetComponent<MeshRenderer>().material;
            foreach (GameObject obj in ReplaceMaterials) 
            { 
                Transform View = obj.transform.Find("Scale/View");
                MeshRenderer[] renderers = View.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer renderer in renderers) renderer.material = orig;
            }
            
            foreach (GameObject obj in ReplaceShaders) 
            { 
                MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>(true);
                foreach (Material mat in renderers.SelectMany(x => x.materials)) mat.shader = orig.shader;
            }
        }
    }
}