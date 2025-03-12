using GUIFramework;
using Valheim.SettingsGui;

namespace kg_Blueprint;

public static class Configs
{
    public static ConfigEntry<bool> InstantBuild;
    public static ConfigEntry<int> BuildTime;
    public static ConfigEntry<int> BlueprintLoadFrameSkip, BlueprintBuildFrameSkip, LoadViewMaxPerFrame, GhostmentPlaceMaxPerFrame;
    public static ConfigEntry<bool> RemoveBlueprintPlacementOnUnequip;
    private static ConfigEntry<string> SaveZDOForPrefabs;
    public static HashSet<int> SaveZDOHashset;
    private static void UpdateHashset() => SaveZDOHashset = [..SaveZDOForPrefabs.Value.Replace(" ", "").Split(',').Select(x => x.GetStableHashCode())];
    public static ConfigEntry<bool> IncludeTrees, IncludeDestructibles;
    public static void Init()
    { 
        //synced
        InstantBuild = kg_Blueprint.config("General", "InstantBuild", false, "Instantly build blueprints when they are placed");
        BuildTime = kg_Blueprint.config("General", "BuildTime", 30, "Time in seconds it takes to build a blueprint (if InstantBuild is false)");
        SaveZDOForPrefabs = kg_Blueprint.config("General", "SaveZDOForPrefabs", "MarketPlaceNPC,Sign", "Save ZDOs for prefabs with the given name (comma separated)");
        IncludeTrees = kg_Blueprint.config("General", "IncludeTrees", true, "Include trees in blueprints");
        IncludeDestructibles = kg_Blueprint.config("General", "IncludeDestructibles", true, "Include destructibles in blueprints");
        UpdateHashset();
        SaveZDOForPrefabs.SettingChanged += (_, _) => UpdateHashset();
        //local
        BlueprintLoadFrameSkip = kg_Blueprint._thistype.Config.Bind("General", "BlueprintLoadFrameSkip", 4, "Number of frames to skip when loading a blueprint");
        BlueprintBuildFrameSkip = kg_Blueprint._thistype.Config.Bind("General", "BlueprintBuildFrameSkip", 4, "Number of frames to skip when building a blueprint");
        LoadViewMaxPerFrame = kg_Blueprint._thistype.Config.Bind("General", "LoadViewMaxPerFrame", 20, "Maximum number of objects to load per frame when viewing a blueprint");
        GhostmentPlaceMaxPerFrame = kg_Blueprint._thistype.Config.Bind("General", "GhostmentPlaceMaxPerFrame", 10, "Maximum number of ghost objects loaded per frame (placement)");
        RemoveBlueprintPlacementOnUnequip = kg_Blueprint._thistype.Config.Bind("General", "RemoveBlueprintPlacementOnUnequip", false, "Remove the ghost object when the blueprint is unequipped");
    }
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
    static class Menu_Start_Patch
    {
        private static bool firstInit = true; 
        [UsedImplicitly]
        private static void Postfix(FejdStartup __instance)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return; 
            if (!firstInit) return;
            firstInit = false;
            GameObject settingsPrefab = __instance.m_settingsPrefab;
            Transform gameplay = settingsPrefab.transform.Find("Panel/TabButtons/Gameplay");
            if (!gameplay) gameplay = settingsPrefab.transform.Find("Panel/TabButtons/Tabs/Gameplay");
            if (!gameplay) return;
            Transform newButton = Object.Instantiate(gameplay);
            newButton.transform.Find("KeyHint").gameObject.SetActive(false); 
            newButton.SetParent(gameplay.parent, false); 
            newButton.name = "kg_Blueprint";
            newButton.SetAsLastSibling();
            Transform textTransform = newButton.transform.Find("Label");
            Transform textTransform_Selected = newButton.transform.Find("Selected/LabelSelected");
            if (!textTransform || !textTransform_Selected) return;
            textTransform.GetComponent<TMP_Text>().text = "$kg_blueprint".Localize();
            textTransform_Selected.GetComponent<TMP_Text>().text = "$kg_blueprint".Localize();
            TabHandler tabHandler = settingsPrefab.transform.Find("Panel/TabButtons").GetComponent<TabHandler>();
            Transform page = settingsPrefab.transform.Find("Panel/TabContent");
            GameObject newPage = Object.Instantiate(kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintSettings"));
            newPage.AddComponent<BlueprintSettings>();
            Localization.instance.Localize(newPage.transform);
            newPage.transform.SetParent(page);  
            newPage.name = "kg_Blueprint";
            newPage.SetActive(false);
            TabHandler.Tab newTab = new TabHandler.Tab
            { 
                m_default = false,
                m_button = newButton.GetComponent<Button>(),
                m_page = newPage.GetComponent<RectTransform>()
            };
            tabHandler.m_tabs.Add(newTab);
        }
    }
    public class BlueprintSettings : SettingsBase
    {
        public override void FixBackButtonNavigation(Button backButton){}
        public override void FixOkButtonNavigation(Button okButton) {}
        private GuiToggle RemoveBlueprintPlacementOnUnequip;
        private Slider BlueprintLoadFrameSkip, BlueprintBuildFrameSkip, LoadViewMaxPerFrame, GhostmentPlaceMaxPerFrame;
        public override void LoadSettings()
        {
            RemoveBlueprintPlacementOnUnequip = this.transform.Find("List/RemoveBlueprintPlacementOnUnequip").GetComponent<GuiToggle>();
            BlueprintLoadFrameSkip = this.transform.Find("List/BlueprintLoadFrameSkip/Slider").GetComponent<Slider>();
            BlueprintBuildFrameSkip = this.transform.Find("List/BlueprintBuildFrameSkip/Slider").GetComponent<Slider>();
            LoadViewMaxPerFrame = this.transform.Find("List/LoadViewMaxPerFrame/Slider").GetComponent<Slider>();
            GhostmentPlaceMaxPerFrame = this.transform.Find("List/GhostmentPlaceMaxPerFrame/Slider").GetComponent<Slider>();
            
            RemoveBlueprintPlacementOnUnequip.isOn = Configs.RemoveBlueprintPlacementOnUnequip.Value;
            BlueprintLoadFrameSkip.onValueChanged.AddListener((float value) =>
            {
                var tmp_value = BlueprintLoadFrameSkip.transform.parent.Find("Value").GetComponent<TMP_Text>();
                tmp_value.text = ((int)value).ToString(); 
            });
            BlueprintLoadFrameSkip.value = Configs.BlueprintLoadFrameSkip.Value;
            BlueprintBuildFrameSkip.onValueChanged.AddListener((float value) =>
            { 
                var tmp_value = BlueprintBuildFrameSkip.transform.parent.Find("Value").GetComponent<TMP_Text>();
                tmp_value.text = ((int)value).ToString();
            }); 
            BlueprintBuildFrameSkip.value = Configs.BlueprintBuildFrameSkip.Value;
            LoadViewMaxPerFrame.onValueChanged.AddListener((float value) =>
            {
                var tmp_value = LoadViewMaxPerFrame.transform.parent.Find("Value").GetComponent<TMP_Text>();
                tmp_value.text = ((int)value).ToString();
            });
            LoadViewMaxPerFrame.value = Configs.LoadViewMaxPerFrame.Value;
            GhostmentPlaceMaxPerFrame.onValueChanged.AddListener((float value) =>
            {
                var tmp_value = GhostmentPlaceMaxPerFrame.transform.parent.Find("Value").GetComponent<TMP_Text>();
                tmp_value.text = ((int)value).ToString();
            });
            GhostmentPlaceMaxPerFrame.value = Configs.GhostmentPlaceMaxPerFrame.Value;
        }
        public override void SaveSettings()
        { 
            Configs.RemoveBlueprintPlacementOnUnequip.Value = RemoveBlueprintPlacementOnUnequip.isOn;
            Configs.BlueprintLoadFrameSkip.Value = (int)BlueprintLoadFrameSkip.value;
            Configs.BlueprintBuildFrameSkip.Value = (int)BlueprintBuildFrameSkip.value;
            Configs.LoadViewMaxPerFrame.Value = (int)LoadViewMaxPerFrame.value;
            Configs.GhostmentPlaceMaxPerFrame.Value = (int)GhostmentPlaceMaxPerFrame.value;
            Saved();  
        }
    }
}   