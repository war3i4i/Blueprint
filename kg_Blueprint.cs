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
    private void Awake()
    {
        _thistype = this;
        Localizer.Load();
        if (!Directory.Exists(BlueprintsPath)) Directory.CreateDirectory(BlueprintsPath); 
        new BuildPiece(Asset, "kg_BlueprintBox").Prefab.AddComponent<BlueprintPiece>();
        new Item(Asset, "kg_BlueprintHammer"){ Configurable = Configurability.Recipe };
        Configs.Init();
        BlueprintUI.Init();
        BuildProgress.Init();
        ReadBlueprints();
        new Harmony(GUID).PatchAll();
    }
    private void FixedUpdate() => PlayerState.Update();
    private void Update() { if (Input.GetKeyDown(KeyCode.Escape) && BlueprintUI.IsVisible) BlueprintUI.Hide(); }
    private static AssetBundle GetAssetBundle(string filename)
    {
        Assembly execAssembly = Assembly.GetExecutingAssembly(); 
        string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(filename));
        using Stream stream = execAssembly.GetManifestResourceStream(resourceName)!;
        return AssetBundle.LoadFromStream(stream);
    }
    public static void ReadBlueprints()
    {
        List<BlueprintRoot> Blueprints = [];
        Deserializer deserializer = new Deserializer(); 
        string[] files = Directory.GetFiles(BlueprintsPath, "*.yml", SearchOption.AllDirectories);
        if (files.Length == 0) return;
        for (int i = 0; i < files.Length; ++i)
        {
            try
            {
                BlueprintRoot root = deserializer.Deserialize<BlueprintRoot>(File.ReadAllText(files[i]));
                if (!root.IsValid(out string reason))
                {
                    Logger.LogError($"Blueprint {files[i]} is invalid: {reason}");
                    continue;
                }
                root.AssignPath(files[i]);
                Blueprints.Add(root);
            }
            catch(Exception e)
            {
                Logger.LogError($"Error reading blueprint {files[i]}: {e}");
            }
        }
        BlueprintUI.Load(Blueprints);
    }
        
}