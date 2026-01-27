using PureDOTS.Runtime.Config;

namespace PureDOTS.Systems
{
    internal static class ManualPhaseConfig
    {
        [RuntimeConfigVar("phase.camera.enabled", "1", Flags = RuntimeConfigFlags.Save, Description = "Enable the camera phase group.")]
        public static RuntimeConfigVar CameraPhaseToggle;

        [RuntimeConfigVar("phase.transport.enabled", "1", Flags = RuntimeConfigFlags.Save, Description = "Enable the transport/logistics phase group.")]
        public static RuntimeConfigVar TransportPhaseToggle;

        [RuntimeConfigVar("phase.history.enabled", "1", Flags = RuntimeConfigFlags.Save, Description = "Enable history capture phase group.")]
        public static RuntimeConfigVar HistoryPhaseToggle;
    }
}


