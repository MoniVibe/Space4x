using PureDOTS.Runtime.Config;

namespace PureDOTS.Runtime.Debugging
{
    public static class DebugConfigVars
    {
        [RuntimeConfigVar("overlay.runtime.enabled", "0", Flags = RuntimeConfigFlags.Save, Description = "Show the runtime diagnostics overlay (camera, phases, physics history).")]
        public static RuntimeConfigVar DiagnosticsOverlayEnabled;
    }
}


