namespace kg_Blueprint;

public static class Configs
{
    public static ConfigEntry<bool> AutoAddBlueprintsToUI;
    public static void Init()
    {
        AutoAddBlueprintsToUI = kg_Blueprint._thistype.Config.Bind("General", "AutoAddBlueprintsToUI", true, "Automatically add blueprints to the UI when they are created");
    }
}