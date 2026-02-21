using PureDOTS.Runtime.Scenarios;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using Space4X.Camera;

namespace Space4x.Scenario
{
    [DisallowMultipleComponent]
    public sealed class Space4XFleetcrawlCameraFollowMono : MonoBehaviour
    {
        [SerializeField] private float minDistance = 35f;
        [SerializeField] private float maxDistance = 180f;
        [SerializeField] private float startDistance = 82f;
        [SerializeField] private float followLerp = 6f;
        [SerializeField] private float lookLerp = 8f;
        [SerializeField] private float zoomSpeed = 0.06f;
        [SerializeField] private float heightRatio = 0.56f;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _flagshipQuery;
        private bool _queriesReady;
        private float _distance;
        private bool _disabledRig;

        private void Awake()
        {
            _distance = math.clamp(startDistance, minDistance, maxDistance);
        }

        private void LateUpdate()
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

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            if (!_disabledRig && cam.TryGetComponent<Space4XCameraRigController>(out var rig))
            {
                rig.enabled = false;
                _disabledRig = true;
            }

            var scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            _distance = math.clamp(_distance - scroll * zoomSpeed, minDistance, maxDistance);

            var flagshipEntity = _flagshipQuery.GetSingletonEntity();
            var flagshipPosition = _entityManager.GetComponentData<LocalTransform>(flagshipEntity).Position;
            var desiredPosition = new Vector3(
                flagshipPosition.x,
                flagshipPosition.y + _distance * heightRatio,
                flagshipPosition.z - _distance);

            cam.transform.position = Vector3.Lerp(cam.transform.position, desiredPosition, Mathf.Clamp01(followLerp * Time.deltaTime));
            var lookTarget = new Vector3(flagshipPosition.x, flagshipPosition.y + 2f, flagshipPosition.z);
            var lookRotation = Quaternion.LookRotation((lookTarget - cam.transform.position).normalized, Vector3.up);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, lookRotation, Mathf.Clamp01(lookLerp * Time.deltaTime));
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
                ComponentType.ReadOnly<LocalTransform>());
            _queriesReady = true;
            return true;
        }
    }
}
