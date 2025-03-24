using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ItemDataManager;
using ItemManager;
using LocalizationManager;
using PieceManager;
using ServerSync;
using CraftingTable = ItemManager.CraftingTable;
using Debug = System.Diagnostics.Debug;

namespace kg_Blueprint;

[BepInPlugin(GUID, NAME, VERSION)] 
public class kg_Blueprint : BaseUnityPlugin 
{ 
    public static kg_Blueprint _thistype;
    private const string GUID = "kg.Blueprint";
    private const string NAME = "Blueprint";
    private const string VERSION = "1.5.0";
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
    public static PieceTable Blueprint_PT;
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
        /*BuildPiece sharingPiece = new BuildPiece(Asset, "kg_BlueprintSharing");
        sharingPiece.Prefab.AddComponent<BlueprintSharing>();
        sharingPiece.RequiredItems.Add("Wood", 30, true);
        sharingPiece.RequiredItems.Add("Iron", 5, true);
        ReplaceShaders.Add(sharingPiece.Prefab); */
        Item blueprintHammer = new Item(Asset, "kg_BlueprintHammer"){ Configurable = Configurability.Recipe };
        blueprintHammer.RequiredItems.Add("Wood", 10);
        blueprintHammer.RequiredItems.Add("Blueberries", 5);
        blueprintHammer.Crafting.Add(CraftingTable.Inventory, 1);
        Blueprint_PT = blueprintHammer.Prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces;
        Item blueprintBook = new Item(Asset, "kg_BlueprintBook"){ Configurable = Configurability.Recipe };
        blueprintBook.Prefab.GetComponent<ItemDrop>().m_itemData.Data().Add<BlueprintItemDataSource>();
        ItemData.RegisterOverrideDescription(typeof(BlueprintItemDataSource));
        /*blueprintBook.RequiredItems.Add("Wood", 5);
        blueprintBook.RequiredItems.Add("Blueberries", 1); 
        blueprintBook.Crafting.Add(CraftingTable.Inventory, 1);*/
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
    private static object _lock = new object();
    private static BlueprintRoot ProcessBlueprint(string file, IDeserializer deserializer)
    {
        string ext = Path.GetExtension(file);
        BlueprintRoot root = null;
        switch (ext)
        {
            case ".blueprint": 
                root = PlanbuildParser.Parse(File.ReadAllLines(file));
                root.SetCategory(".blueprint#4e82ea");
                break;
            case ".vbuild":
                string fNameNoExt = Path.GetFileNameWithoutExtension(file);
                root = VBuildParser.Parse(fNameNoExt, File.ReadAllLines(file));
                root.SetCategory(".vbuild#f48b75");
                break; 
            case ".yml":
                root = deserializer.Deserialize<BlueprintRoot>(File.ReadAllText(file));
                string parentFolderName = Path.GetFileName(Path.GetDirectoryName(file));
                if (parentFolderName != "Blueprints") root.SetCategory(parentFolderName);
                break;
            case ".oprint":
                root = new();
                root.DeserializeFull(File.ReadAllBytes(file));
                parentFolderName = Path.GetFileName(Path.GetDirectoryName(file));
                if (parentFolderName != "Blueprints") root.SetCategory(parentFolderName);
                break;
        }
        return root;
    }
    public static void ReadBlueprints()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;
        Task.Run(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                Stopwatch stopwatch = Stopwatch.StartNew();
                string[] search = Directory.GetFiles(BlueprintsPath, "*.yml", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(BlueprintsPath, "*.blueprint", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(BlueprintsPath, "*.vbuild", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(BlueprintsPath, "*.oprint", SearchOption.AllDirectories))
                    .ToArray();
                Logger.LogDebug($"Found {search.Length} blueprints");
                List<BlueprintRoot> Blueprints = new(search.Length);
                if (Configs.UseMultithreadIO.Value)
                {
                    const int maxproc = 4; 
                    string[][] chunks = search.SplitIntoChunksWeightLoad(maxproc);
                    int maxChunkSize = chunks.Max(x => x.Length);
                    Logger.LogDebug($"Using multithread IO with {maxproc} threads and chunk size {maxChunkSize}. Chunk count: {chunks.Length}");
                    Parallel.ForEach(chunks, new ParallelOptions { CancellationToken = token}, files =>
                    {
                        List<BlueprintRoot> chunkBlueprints = new List<BlueprintRoot>(maxChunkSize);
                        IDeserializer deserializer = new DeserializerBuilder().WithTypeConverter(new IntOrStringConverter()).Build();
                        foreach (string file in files)
                        {
                            token.ThrowIfCancellationRequested();
                            try 
                            {
                                BlueprintRoot root = ProcessBlueprint(file, deserializer);
                                if (!root.IsValid(out string reason))
                                {
                                    Logger.LogError($"Blueprint {file} is invalid: {reason}");
                                    return;
                                }
                                root.AssignPath(file, true);
                                chunkBlueprints.Add(root);
                            }
                            catch (Exception e)
                            {
                                Logger.LogError($"Error reading blueprint {file}: {e}");
                            }
                        }
                        lock (_lock) Blueprints.AddRange(chunkBlueprints);
                    });
                }
                else
                {
                    IDeserializer deserializer = new DeserializerBuilder().WithTypeConverter(new IntOrStringConverter()).Build();
                    for (int i = 0; i < search.Length; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        string file = search[i];
                        try
                        {
                            BlueprintRoot root = ProcessBlueprint(file, deserializer);
                            if (!root.IsValid(out string reason))
                            {
                                Logger.LogError($"Blueprint {file} is invalid: {reason}");
                                return;
                            }
                            root.AssignPath(file, true);
                        }
                        catch (Exception e)
                        {
                            Logger.LogError($"Error reading blueprint {file}: {e}");
                        }
                    }
                }
                kg_Blueprint.Logger.LogDebug($"[Multithread] Loaded blueprints in {stopwatch.ElapsedMilliseconds}ms. Blueprint count: {Blueprints.Count}");
                token.ThrowIfCancellationRequested();
                ThreadingHelper.Instance.StartSyncInvoke(() => BlueprintUI.Load(Blueprints));
            }
            catch (Exception ex)
            { 
                if (ex is OperationCanceledException) Logger.LogDebug("Blueprint loading canceled.");
                else Logger.LogError($"Error loading blueprints {ex}");
                ThreadingHelper.Instance.StartSyncInvoke(() => BlueprintUI.Load([]));
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