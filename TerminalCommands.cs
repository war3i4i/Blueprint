namespace kg_Blueprint;

public static class TerminalCommands
{
    [HarmonyPatch(typeof(Terminal),nameof(Terminal.InitTerminal))]
    private static class Terminal_InitTerminal_Patch
    {
        [UsedImplicitly] private static void Postfix()
        {
            new Terminal.ConsoleCommand("reloadbp", "Reloads the blueprint list", (args) =>
            {
                kg_Blueprint.ReadBlueprints();
                args.Context.AddString("<color=green>Blueprint list reloaded</color>");
            });
            new Terminal.ConsoleCommand("reloadblueprints", "Reloads the blueprint list", (args) =>
            {
                kg_Blueprint.ReadBlueprints();
                args.Context.AddString("<color=green>Blueprint list reloaded</color>");
            });
        }
    }
}