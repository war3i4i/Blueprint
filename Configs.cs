namespace kg_Blueprint;

public static class Configs
{
    public static ConfigEntry<bool> AutoAddBlueprintsToUI;
    public static ConfigEntry<bool> InstantBuild;
    public static ConfigEntry<int> BuildTime;
    public static ConfigEntry<int> BlueprintLoadFrameSkip, BlueprintBuildFrameSkip;
    public static ConfigEntry<bool> RemoveBlueprintPlacementOnUnequip;
    public static void Init()
    {
        AutoAddBlueprintsToUI = kg_Blueprint._thistype.Config.Bind("General", "AutoAddBlueprintsToUI", true, "Automatically add blueprints to the UI when they are created");
        InstantBuild = kg_Blueprint._thistype.Config.Bind("General", "InstantBuild", false, "Instantly build blueprints when they are placed");
        BuildTime = kg_Blueprint._thistype.Config.Bind("General", "BuildTime", 30, "Time in seconds it takes to build a blueprint (if InstantBuild is false)");
        BlueprintLoadFrameSkip = kg_Blueprint._thistype.Config.Bind("General", "BlueprintLoadFrameSkip", 4, "Number of frames to skip when loading a blueprint");
        BlueprintBuildFrameSkip = kg_Blueprint._thistype.Config.Bind("General", "BlueprintBuildFrameSkip", 4, "Number of frames to skip when building a blueprint");
        RemoveBlueprintPlacementOnUnequip = kg_Blueprint._thistype.Config.Bind("General", "RemoveBlueprintPlacementOnUnequip", true, "Remove the ghost object when the blueprint is unequipped");
    }
}  