#nullable enable
#if CAMERA_RIG_ENABLED
using PureDOTS.Runtime.Camera;
#endif
using PureDOTS.Runtime.Config;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Ensures the ManualPhaseControl singleton exists with sensible defaults.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [CreateAfter(typeof(CoreSingletonBootstrapSystem))]
    [CreateAfter(typeof(RuntimeConfigBootstrapSystem))]
    public sealed partial class ManualPhaseBootstrapSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();

            var entityManager = EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ManualPhaseControl>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(ManualPhaseControl));
                var profile = SystemRegistry.ResolveActiveProfile();
                entityManager.SetComponentData(entity, ManualPhaseControl.CreateDefaults(profile.Id));
            }

            Enabled = false;
        }

        protected override void OnUpdate()
        {
            // Intentionally empty; system runs only once in OnCreate.
        }
    }

    /// <summary>
    /// Applies ManualPhaseControl toggles to the concrete phase groups each frame.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(ManualPhaseBootstrapSystem))]
    public sealed partial class ManualPhaseControllerSystem : SystemBase
    {
        private CameraPhaseGroup? _cameraGroup;
        private TransportPhaseGroup? _transportGroup;
        private HistoryPhaseGroup? _historyGroup;
        private RuntimeConfigVar? _cameraVar;
        private RuntimeConfigVar? _transportVar;
        private RuntimeConfigVar? _historyVar;
        private RuntimeConfigVar? _cameraModeVar;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<ManualPhaseControl>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _cameraGroup = World.GetExistingSystemManaged<CameraPhaseGroup>();
            _transportGroup = World.GetExistingSystemManaged<TransportPhaseGroup>();
            _historyGroup = World.GetExistingSystemManaged<HistoryPhaseGroup>();
            RuntimeConfigRegistry.Initialize();
            _cameraVar = ManualPhaseConfig.CameraPhaseToggle;
#if CAMERA_RIG_ENABLED
            _cameraModeVar = CameraConfigVars.EcsModeEnabled;
#else
            _cameraModeVar = null;
#endif
            _transportVar = ManualPhaseConfig.TransportPhaseToggle;
            _historyVar = ManualPhaseConfig.HistoryPhaseToggle;
        }

        protected override void OnUpdate()
        {
            _cameraGroup ??= World.GetExistingSystemManaged<CameraPhaseGroup>();
            _transportGroup ??= World.GetExistingSystemManaged<TransportPhaseGroup>();
            _historyGroup ??= World.GetExistingSystemManaged<HistoryPhaseGroup>();

            var control = SystemAPI.GetSingleton<ManualPhaseControl>();
            var originalControl = control;

            var ecsEnabled = _cameraModeVar != null && _cameraModeVar.BoolValue;
            var cameraPhaseToggle = _cameraVar != null && _cameraVar.BoolValue;

            var desiredCameraPhase = ecsEnabled && cameraPhaseToggle;
            if (control.CameraPhaseEnabled != desiredCameraPhase)
            {
                control.CameraPhaseEnabled = desiredCameraPhase;
            }

            if (_transportVar != null)
            {
                var desired = _transportVar.BoolValue;
                if (control.TransportPhaseEnabled != desired)
                {
                    control.TransportPhaseEnabled = desired;
                }
            }

            if (_historyVar != null)
            {
                var desired = _historyVar.BoolValue;
                if (control.HistoryPhaseEnabled != desired)
                {
                    control.HistoryPhaseEnabled = desired;
                }
            }

            if (!control.Equals(originalControl))
            {
                SystemAPI.SetSingleton(control);
            }

            if (_cameraGroup is { } camera)
            {
                camera.Enabled = control.CameraPhaseEnabled;
            }

            if (_transportGroup is { } transport)
            {
                transport.Enabled = control.TransportPhaseEnabled;
            }

            if (_historyGroup is { } history)
            {
                history.Enabled = control.HistoryPhaseEnabled;
            }
        }
    }
}

