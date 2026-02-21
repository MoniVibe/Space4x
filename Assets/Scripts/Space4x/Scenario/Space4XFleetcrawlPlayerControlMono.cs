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
        [SerializeField] private float moveSpeed = 95f;
        [SerializeField] private float boostMultiplier = 1.9f;
        [SerializeField] private float dashDistance = 24f;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _flagshipQuery;
        private bool _queriesReady;

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

            var moveInput = new float2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            if (math.lengthsq(moveInput) < 1e-5f)
            {
                return;
            }

            var flagshipEntity = _flagshipQuery.GetSingletonEntity();
            var transform = _entityManager.GetComponentData<LocalTransform>(flagshipEntity);
            var command = _entityManager.GetComponentData<MovementCommand>(flagshipEntity);
            var basis = ResolveMoveBasis();
            var moveDir = math.normalize(basis.right * moveInput.x + basis.forward * moveInput.y);
            var speed = moveSpeed * (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? boostMultiplier : 1f);
            var step = speed * Time.deltaTime;
            if (keyboard.qKey.wasPressedThisFrame)
            {
                step += dashDistance;
            }

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
