using Unity.Entities;
#if CAMERA_RIG_ENABLED
using PureDOTS.Runtime.Camera;
#endif

namespace PureDOTS.Systems
{
    /// <summary>
    /// Singleton used to toggle manual phase groups on or off at runtime.
    /// </summary>
    public struct ManualPhaseControl : IComponentData
    {
        public bool CameraPhaseEnabled;
        public bool TransportPhaseEnabled;
        public bool HistoryPhaseEnabled;

        public static ManualPhaseControl CreateDefaults(string profileId)
        {
            var defaults = new ManualPhaseControl
            {
#if CAMERA_RIG_ENABLED
                CameraPhaseEnabled = CameraRigService.IsEcsCameraEnabled,
#else
                CameraPhaseEnabled = false,
#endif
                TransportPhaseEnabled = true,
                HistoryPhaseEnabled = true
            };

            if (profileId == SystemRegistry.BuiltinProfiles.HeadlessId)
            {
                defaults.CameraPhaseEnabled = false;
            }

            return defaults;
        }
    }
}
