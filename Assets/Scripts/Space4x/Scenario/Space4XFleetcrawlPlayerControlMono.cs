using PureDOTS.Runtime.Scenarios;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4x.Scenario
{
    [DisallowMultipleComponent]
    public sealed class Space4XFleetcrawlPlayerControlMono : MonoBehaviour
    {
        public struct StatusSnapshot
        {
            public float Boost01;
            public float DashCooldown;
            public float Speed;
        }

        [SerializeField] private float moveSpeed = 95f;
        [SerializeField] private float boostMultiplier = 1.9f;
        [SerializeField] private float acceleration = 11f;
        [SerializeField] private float deceleration = 13f;
        [SerializeField] private float boostDrainPerSecond = 0.55f;
        [SerializeField] private float boostRecoverPerSecond = 0.30f;
        [SerializeField] private float boostMinToActivate = 0.12f;
        [SerializeField] private float dashDistance = 24f;
        [SerializeField] private float dashCooldownSeconds = 1.1f;
        [SerializeField] private float dashBurstSeconds = 0.2f;
        [SerializeField] private float dashBurstMultiplier = 2.4f;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _flagshipQuery;
        private bool _queriesReady;
        private float _smoothedSpeed;
        private float _boostEnergy = 1f;
        private float _dashCooldownRemaining;
        private float _dashBurstRemaining;
        private float3 _lastMoveDir = new float3(0f, 0f, 1f);

        public static StatusSnapshot CurrentStatus { get; private set; }

        private void Update()
        {
            if (!TryEnsureQueries())
            {
                return;
            }

            if (_scenarioQuery.IsEmptyIgnoreFilter || _flagshipQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var scenarioInfo = _scenarioQuery.GetSingleton<ScenarioInfo>();
            if (!Space4XFleetcrawlUiBridge.IsFleetcrawlScenario(scenarioInfo.ScenarioId))
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var dt = Time.deltaTime;
            _dashCooldownRemaining = math.max(0f, _dashCooldownRemaining - dt);
            _dashBurstRemaining = math.max(0f, _dashBurstRemaining - dt);

            var moveInput = new float2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            var hasInput = math.lengthsq(moveInput) > 1e-5f;
            var basis = ResolveMoveBasis();
            var moveDir = hasInput ? math.normalize(basis.right * moveInput.x + basis.forward * moveInput.y) : _lastMoveDir;
            if (hasInput)
            {
                _lastMoveDir = moveDir;
            }

            var boostPressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            var boosting = boostPressed && hasInput && _boostEnergy > boostMinToActivate;
            if (boosting)
            {
                _boostEnergy = math.max(0f, _boostEnergy - boostDrainPerSecond * dt);
            }
            else
            {
                _boostEnergy = math.min(1f, _boostEnergy + boostRecoverPerSecond * dt);
            }

            if (keyboard.qKey.wasPressedThisFrame && _dashCooldownRemaining <= 0f)
            {
                _dashCooldownRemaining = dashCooldownSeconds;
                _dashBurstRemaining = dashBurstSeconds;
                Debug.Log($"[FleetcrawlUI] DASH trigger cooldown={dashCooldownSeconds:0.00}s.");
            }

            var targetSpeed = hasInput ? moveSpeed : 0f;
            if (boosting)
            {
                targetSpeed *= boostMultiplier;
            }
            if (_dashBurstRemaining > 0f)
            {
                targetSpeed *= dashBurstMultiplier;
            }

            var accel = hasInput ? acceleration : deceleration;
            _smoothedSpeed = math.lerp(_smoothedSpeed, targetSpeed, math.saturate(accel * dt));
            var step = _smoothedSpeed * dt;

            if (_dashBurstRemaining > 0f && hasInput)
            {
                step += dashDistance * dt * 0.35f;
            }

            if (!hasInput && _smoothedSpeed < 0.05f)
            {
                UpdateStatusSnapshot();
                return;
            }

            var flagshipEntity = _flagshipQuery.GetSingletonEntity();
            var transform = _entityManager.GetComponentData<LocalTransform>(flagshipEntity);
            var command = _entityManager.GetComponentData<MovementCommand>(flagshipEntity);

            var target = transform.Position + moveDir * step;
            target.y = transform.Position.y;
            command.TargetPosition = target;
            command.ArrivalThreshold = 0.2f;
            _entityManager.SetComponentData(flagshipEntity, command);

            if (_entityManager.HasComponent<Carrier>(flagshipEntity))
            {
                var carrier = _entityManager.GetComponentData<Carrier>(flagshipEntity);
                carrier.PatrolCenter = target;
                _entityManager.SetComponentData(flagshipEntity, carrier);
            }

            UpdateStatusSnapshot();
        }

        private void UpdateStatusSnapshot()
        {
            CurrentStatus = new StatusSnapshot
            {
                Boost01 = _boostEnergy,
                DashCooldown = _dashCooldownRemaining,
                Speed = _smoothedSpeed
            };
        }

        private bool TryEnsureQueries()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            if (_queriesReady && world == _world)
            {
                return true;
            }

            _world = world;
            _entityManager = world.EntityManager;
            _scenarioQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioInfo>());
            _flagshipQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerFlagshipTag>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<MovementCommand>());
            _queriesReady = true;
            return true;
        }

        private static (float3 forward, float3 right) ResolveMoveBasis()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return (new float3(0f, 0f, 1f), new float3(1f, 0f, 0f));
            }

            var forward = cam.transform.forward;
            var right = cam.transform.right;
            forward.y = 0f;
            right.y = 0f;

            var forwardLen = forward.sqrMagnitude;
            var rightLen = right.sqrMagnitude;
            if (forwardLen < 1e-6f || rightLen < 1e-6f)
            {
                return (new float3(0f, 0f, 1f), new float3(1f, 0f, 0f));
            }

            return (((Vector3)forward).normalized, ((Vector3)right).normalized);
        }
    }
}
