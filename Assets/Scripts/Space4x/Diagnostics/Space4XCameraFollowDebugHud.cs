#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Space4X.Registry;
using Space4X.UI;
using Space4X.Runtime;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UCamera = UnityEngine.Camera;
using UTime = UnityEngine.Time;

namespace Space4X.Diagnostics
{
    /// <summary>
    /// Runtime camera/follow diagnostics for gameplay mode debugging (F6 toggles visibility).
    /// </summary>
    [DefaultExecutionOrder(-9400)]
    [DisallowMultipleComponent]
    public sealed class Space4XCameraFollowDebugHud : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private Key toggleKey = Key.F6;
        [SerializeField] private Rect panelRect = new Rect(12f, 164f, 760f, 352f);
        [SerializeField] private int fontSize = 12;

        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private bool _hasTickBaseline;
        private uint _lastTick;
        private bool _hasTargetKinematicsBaseline;
        private Entity _lastKinematicsTarget;
        private Vector3 _lastKinematicsPosition;
        private float _lastKinematicsSampleAt;
        private float _lastKinematicsSpeed;
        private bool _hasPoseParityBaseline;
        private Entity _lastPoseParityTarget;
        private Vector3 _lastPoseParityLocalPosition;
        private Vector3 _lastPoseParityWorldPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode)
                return;

            if (Object.FindFirstObjectByType<Space4XCameraFollowDebugHud>() != null)
                return;

            var go = new GameObject("Space4X Camera Follow Debug HUD");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Space4XCameraFollowDebugHud>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();

            var camera = UCamera.main ?? Object.FindFirstObjectByType<UCamera>();
            var follow = camera != null ? camera.GetComponent<Space4XFollowPlayerVessel>() : null;
            var controller = camera != null ? camera.GetComponent<Space4XPlayerFlagshipController>() : null;
            var controlMode = Space4XControlModeState.CurrentMode;

            var controlledEntity = Entity.Null;
            var targetEntity = Entity.Null;
            var hasControlled = controller != null && controller.TryGetControlledFlagship(out controlledEntity);
            var hasTarget = follow != null && follow.TryGetDebugTarget(out targetEntity);
            var entitiesAligned = hasControlled && hasTarget && controlledEntity == targetEntity;
            var hasViewportY = TryGetTargetViewportY(camera, targetEntity, out var viewportY);
            var viewportText = hasViewportY ? viewportY.ToString("0.000") : "n/a";
            var motionFlags = TryGetTargetMotionFlags(targetEntity, out var flagsText) ? flagsText : "n/a";
            var floatingOriginStatus = TryGetFloatingOriginStatus(out var floatingText) ? floatingText : "n/a";
            var cadenceStatus = TryGetCadenceStatus(out var cadenceText) ? cadenceText : "n/a";
            var inputTickStatus = TryGetInputTickStatus(out var inputTickText) ? inputTickText : "n/a";
            var targetKinematicsStatus = TryGetTargetKinematicsStatus(targetEntity, out var targetKinematicsText)
                ? targetKinematicsText
                : "n/a";
            var poseParityStatus = TryGetTargetPoseParityStatus(targetEntity, out var poseParityText)
                ? poseParityText
                : "n/a";
            var cursorDeadZoneStatus = controller != null
                ? $"anchor={(controller.DebugCursorSteerAnchorActive ? 1 : 0)} unlocked={(controller.DebugCursorSteerDeadZoneUnlocked ? 1 : 0)} deadPx={controller.DebugCursorSteerDeadZonePixels:0.0}"
                : "n/a";
            var orbitDeadZoneStatus = follow != null
                ? $"anchor={(follow.DebugOrbitAnchorActive ? 1 : 0)} unlocked={(follow.DebugOrbitDeadZoneUnlocked ? 1 : 0)} deadPx={follow.DebugOrbitDeadZonePixels:0.0}"
                : "n/a";
            var followInterpolationStatus = follow != null
                ? $"cfg={(follow.DebugInterpolateControlledFlagshipPose ? 1 : 0)} active={(follow.DebugControlledPoseInterpolationActive ? 1 : 0)}"
                : "n/a";

            GUILayout.BeginArea(panelRect, GUIContent.none, _panelStyle);
            GUILayout.Label("Camera Follow Debug (F6)", _labelStyle);
            GUILayout.Label($"Mode={controlMode} FollowComp={(follow != null ? "yes" : "no")} ControllerComp={(controller != null ? "yes" : "no")}", _labelStyle);
            GUILayout.Label($"Controlled={FormatEntity(hasControlled ? controlledEntity : Entity.Null)} Target={FormatEntity(hasTarget ? targetEntity : Entity.Null)}", _labelStyle);
            GUILayout.Label($"Aligned={(entitiesAligned ? "yes" : "no")} Camera={FormatVector3(camera != null ? camera.transform.position : Vector3.zero)}", _labelStyle);
            GUILayout.Label($"TargetViewportY={viewportText} (0 bottom, 0.5 center, 1 top)", _labelStyle);
            GUILayout.Label($"MotionFlags={motionFlags}", _labelStyle);
            GUILayout.Label($"InputTicks={inputTickStatus}", _labelStyle);
            GUILayout.Label($"TargetKinematics={targetKinematicsStatus}", _labelStyle);
            GUILayout.Label($"PoseParity={poseParityStatus}", _labelStyle);
            GUILayout.Label($"FollowInterpolation={followInterpolationStatus}", _labelStyle);
            GUILayout.Label($"CursorDeadzone={cursorDeadZoneStatus}", _labelStyle);
            GUILayout.Label($"OrbitDeadzone={orbitDeadZoneStatus}", _labelStyle);
            GUILayout.Label($"FloatingOrigin={floatingOriginStatus}", _labelStyle);
            GUILayout.Label($"Cadence={cadenceStatus}", _labelStyle);
            GUILayout.Label("If Aligned=no while movement works, camera target handoff is broken.", _labelStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null && _labelStyle != null)
                return;

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = fontSize,
                padding = new RectOffset(10, 10, 8, 8)
            };
            _panelStyle.normal.textColor = new Color(0.87f, 0.94f, 1f, 1f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                richText = false
            };
            _labelStyle.normal.textColor = new Color(0.87f, 0.94f, 1f, 1f);
        }

        private static string FormatEntity(Entity entity)
        {
            return entity == Entity.Null
                ? "Entity.Null"
                : $"Entity({entity.Index}:{entity.Version})";
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:0.0}, {value.y:0.0}, {value.z:0.0})";
        }

        private static bool TryGetTargetViewportY(UCamera camera, Entity target, out float viewportY)
        {
            viewportY = 0f;
            if (camera == null || target == Entity.Null)
                return false;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            var entityManager = world.EntityManager;
            if (!entityManager.Exists(target))
                return false;

            Vector3 worldPosition;
            if (entityManager.HasComponent<LocalTransform>(target))
            {
                var local = entityManager.GetComponentData<LocalTransform>(target);
                worldPosition = new Vector3(local.Position.x, local.Position.y, local.Position.z);
            }
            else if (entityManager.HasComponent<LocalToWorld>(target))
            {
                var localToWorld = entityManager.GetComponentData<LocalToWorld>(target);
                worldPosition = new Vector3(localToWorld.Position.x, localToWorld.Position.y, localToWorld.Position.z);
            }
            else
            {
                return false;
            }

            var viewport = camera.WorldToViewportPoint(worldPosition);
            viewportY = viewport.y;
            return true;
        }

        private static bool TryGetTargetMotionFlags(Entity target, out string flags)
        {
            flags = string.Empty;
            if (target == Entity.Null)
                return false;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            var entityManager = world.EntityManager;
            if (!entityManager.Exists(target))
                return false;

            var hasSuppressed = entityManager.HasComponent<MovementSuppressed>(target) &&
                                entityManager.IsComponentEnabled<MovementSuppressed>(target);
            var hasOrbitAnchor = entityManager.HasComponent<Space4XOrbitAnchor>(target);
            var hasOrbitState = entityManager.HasComponent<Space4XOrbitAnchorState>(target);
            var hasRogueOrbit = entityManager.HasComponent<Space4XRogueOrbitTag>(target);
            var hasMicroImpulse = entityManager.HasComponent<Space4XMicroImpulseTag>(target);
            var hasFrameMembership = entityManager.HasComponent<Space4XFrameMembership>(target);
            var hasFrameDriven = entityManager.HasComponent<Space4XFrameDrivenTransformTag>(target);
            var localToWorldDelta = -1f;
            if (entityManager.HasComponent<LocalTransform>(target) && entityManager.HasComponent<LocalToWorld>(target))
            {
                var local = entityManager.GetComponentData<LocalTransform>(target);
                var localToWorld = entityManager.GetComponentData<LocalToWorld>(target);
                var dx = local.Position.x - localToWorld.Position.x;
                var dy = local.Position.y - localToWorld.Position.y;
                var dz = local.Position.z - localToWorld.Position.z;
                localToWorldDelta = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            flags =
                $"suppressed={(hasSuppressed ? 1 : 0)} orbitAnchor={(hasOrbitAnchor ? 1 : 0)} orbitState={(hasOrbitState ? 1 : 0)} rogueOrbit={(hasRogueOrbit ? 1 : 0)} microImpulse={(hasMicroImpulse ? 1 : 0)} frameMembership={(hasFrameMembership ? 1 : 0)} frameDriven={(hasFrameDriven ? 1 : 0)} ltwDelta={(localToWorldDelta >= 0f ? localToWorldDelta.ToString("0.000") : "n/a")}";
            return true;
        }

        private static bool TryGetFloatingOriginStatus(out string status)
        {
            status = string.Empty;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            var entityManager = world.EntityManager;
            using var configQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XFloatingOriginConfig>());
            if (configQuery.IsEmptyIgnoreFilter)
                return false;

            var config = configQuery.GetSingleton<Space4XFloatingOriginConfig>();
            var enabled = config.Enabled != 0 ? 1 : 0;
            var threshold = config.Threshold;
            var cooldown = config.CooldownTicks;

            using var stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XFloatingOriginState>());
            if (!stateQuery.IsEmptyIgnoreFilter)
            {
                var originState = stateQuery.GetSingleton<Space4XFloatingOriginState>();
                var shiftMag = Mathf.Sqrt(
                    originState.LastShift.x * originState.LastShift.x +
                    originState.LastShift.y * originState.LastShift.y +
                    originState.LastShift.z * originState.LastShift.z);
                status =
                    $"enabled={enabled} threshold={threshold:0.0} cooldown={cooldown} lastShiftMag={shiftMag:0.00} lastShiftTick={originState.LastShiftTick}";
                return true;
            }

            status = $"enabled={enabled} threshold={threshold:0.0} cooldown={cooldown} lastShift=n/a";
            return true;
        }

        private bool TryGetCadenceStatus(out string status)
        {
            status = string.Empty;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            var entityManager = world.EntityManager;
            using var timeQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TimeState>(),
                ComponentType.ReadOnly<FixedStepInterpolationState>());
            if (timeQuery.IsEmptyIgnoreFilter)
                return false;

            var timeState = timeQuery.GetSingleton<TimeState>();
            var interpolation = timeQuery.GetSingleton<FixedStepInterpolationState>();
            var tickDelta = 0u;
            if (_hasTickBaseline)
            {
                tickDelta = timeState.Tick >= _lastTick ? timeState.Tick - _lastTick : 0u;
            }

            _lastTick = timeState.Tick;
            _hasTickBaseline = true;
            status =
                $"tick={timeState.Tick} tickDelta={tickDelta} speed={timeState.CurrentSpeedMultiplier:0.00} fixedDt={timeState.FixedDeltaTime:0.0000} alpha={interpolation.Alpha:0.000}";
            return true;
        }

        private static bool TryGetInputTickStatus(out string status)
        {
            status = string.Empty;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerFlagshipInputTickDiagnostics>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            var diagnostics = query.GetSingleton<PlayerFlagshipInputTickDiagnostics>();
            status =
                $"tick={diagnostics.Tick} observed={diagnostics.TickDeltaObserved} processed={diagnostics.TickStepsProcessed} backlog={diagnostics.TickBacklog} maxBacklog={diagnostics.MaxBacklogObserved} fixedDt={diagnostics.FixedDeltaTime:0.0000} speed={diagnostics.SpeedMultiplier:0.00}";
            return true;
        }

        private bool TryGetTargetKinematicsStatus(Entity target, out string status)
        {
            status = string.Empty;
            if (target == Entity.Null)
            {
                _hasTargetKinematicsBaseline = false;
                _lastKinematicsTarget = Entity.Null;
                return false;
            }

            if (!TryGetTargetWorldPosition(target, out var worldPosition))
            {
                _hasTargetKinematicsBaseline = false;
                _lastKinematicsTarget = Entity.Null;
                return false;
            }

            var now = UTime.unscaledTime;
            if (!_hasTargetKinematicsBaseline || _lastKinematicsTarget != target)
            {
                _hasTargetKinematicsBaseline = true;
                _lastKinematicsTarget = target;
                _lastKinematicsPosition = worldPosition;
                _lastKinematicsSampleAt = now;
                _lastKinematicsSpeed = 0f;
                status = "warming";
                return true;
            }

            var dt = Mathf.Max(1e-4f, now - _lastKinematicsSampleAt);
            var step = Vector3.Distance(worldPosition, _lastKinematicsPosition);
            var speed = step / dt;
            var accel = (speed - _lastKinematicsSpeed) / dt;

            _lastKinematicsPosition = worldPosition;
            _lastKinematicsSampleAt = now;
            _lastKinematicsSpeed = speed;

            status = $"step={step:0.000} speed={speed:0.00} accel={accel:0.00}";
            return true;
        }

        private bool TryGetTargetPoseParityStatus(Entity target, out string status)
        {
            status = string.Empty;
            if (target == Entity.Null)
            {
                _hasPoseParityBaseline = false;
                _lastPoseParityTarget = Entity.Null;
                return false;
            }

            if (!TryGetTargetPoseSamples(target, out var localPosition, out var worldPosition, out var hasLocal, out var hasWorld))
            {
                _hasPoseParityBaseline = false;
                _lastPoseParityTarget = Entity.Null;
                return false;
            }

            if (!_hasPoseParityBaseline || _lastPoseParityTarget != target)
            {
                _hasPoseParityBaseline = true;
                _lastPoseParityTarget = target;
                _lastPoseParityLocalPosition = localPosition;
                _lastPoseParityWorldPosition = worldPosition;
                status = "warming";
                return true;
            }

            var localStep = hasLocal ? Vector3.Distance(localPosition, _lastPoseParityLocalPosition) : -1f;
            var worldStep = hasWorld ? Vector3.Distance(worldPosition, _lastPoseParityWorldPosition) : -1f;
            var gap = hasLocal && hasWorld ? Vector3.Distance(localPosition, worldPosition) : -1f;

            _lastPoseParityLocalPosition = localPosition;
            _lastPoseParityWorldPosition = worldPosition;

            status =
                $"localStep={(hasLocal ? localStep.ToString("0.000") : "n/a")} ltwStep={(hasWorld ? worldStep.ToString("0.000") : "n/a")} gap={(gap >= 0f ? gap.ToString("0.000") : "n/a")}";
            return true;
        }

        private static bool TryGetTargetPoseSamples(
            Entity target,
            out Vector3 localPosition,
            out Vector3 worldPosition,
            out bool hasLocal,
            out bool hasWorld)
        {
            localPosition = Vector3.zero;
            worldPosition = Vector3.zero;
            hasLocal = false;
            hasWorld = false;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            var entityManager = world.EntityManager;
            if (!entityManager.Exists(target))
                return false;

            if (entityManager.HasComponent<LocalTransform>(target))
            {
                var local = entityManager.GetComponentData<LocalTransform>(target);
                localPosition = new Vector3(local.Position.x, local.Position.y, local.Position.z);
                hasLocal = true;
            }

            if (entityManager.HasComponent<LocalToWorld>(target))
            {
                var localToWorld = entityManager.GetComponentData<LocalToWorld>(target);
                worldPosition = new Vector3(localToWorld.Position.x, localToWorld.Position.y, localToWorld.Position.z);
                hasWorld = true;
            }

            if (!hasLocal && !hasWorld)
                return false;

            if (!hasLocal)
            {
                localPosition = worldPosition;
            }

            if (!hasWorld)
            {
                worldPosition = localPosition;
            }

            return true;
        }

        private static bool TryGetTargetWorldPosition(Entity target, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            var entityManager = world.EntityManager;
            if (!entityManager.Exists(target))
                return false;

            if (entityManager.HasComponent<LocalTransform>(target))
            {
                var local = entityManager.GetComponentData<LocalTransform>(target);
                worldPosition = new Vector3(local.Position.x, local.Position.y, local.Position.z);
                return true;
            }

            if (entityManager.HasComponent<LocalToWorld>(target))
            {
                var localToWorld = entityManager.GetComponentData<LocalToWorld>(target);
                worldPosition = new Vector3(localToWorld.Position.x, localToWorld.Position.y, localToWorld.Position.z);
                return true;
            }

            return false;
        }
    }
}
#endif
