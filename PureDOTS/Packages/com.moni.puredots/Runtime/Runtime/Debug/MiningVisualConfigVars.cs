using PureDOTS.Runtime.Config;

namespace PureDOTS.Runtime.Debugging
{
    public static class MiningVisualConfigVars
    {
        [RuntimeConfigVar("visuals.mining.enabled", "1", Flags = RuntimeConfigFlags.Save, Description = "Enable mining loop visualisation prefabs.")]
        public static RuntimeConfigVar VisualsEnabled;

        [RuntimeConfigVar("visuals.mining.hud.enabled", "0", Flags = RuntimeConfigFlags.Save, Description = "Show HUD overlays for mining throughput stats.")]
        public static RuntimeConfigVar HudEnabled;
    }
}

