using PureDOTS.Runtime.Config;

namespace PureDOTS.Runtime.Camera
{
    public static class CameraConfigVars
    {
        [RuntimeConfigVar("camera.ecs.enabled", "0", Flags = RuntimeConfigFlags.Save, Description = "Enable the ECS-based camera pipeline.")]
        public static RuntimeConfigVar EcsModeEnabled;
    }
}


