using PureDOTS.Runtime.Scenarios;
using Space4X.Camera;
using Space4X.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] private float zoomStepPerNotch = 0.06f;
        [SerializeField] private float heightRatio = 0.56f;
        [SerializeField] private float leadSeconds = 0.35f;
        [SerializeField] private Key snapToPlayerKey = Key.F;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _flagshipQuery;
        private bool _queriesReady;
        private float _zoomT;
        private bool _disabledRig;

        private void Awake()
        {
            _zoomT = DistanceToZoomT(startDistance);
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
            _zoomT = math.saturate(_zoomT - scroll * zoomStepPerNotch * 0.01f);
            var distance = ZoomDistanceFromT(_zoomT);

            var flagshipEntity = _flagshipQuery.GetSingletonEntity();
            var flagshipPosition = _entityManager.GetComponentData<LocalTransform>(flagshipEntity).Position;
            var velocity = _entityManager.HasComponent<VesselMovement>(flagshipEntity)
                ? _entityManager.GetComponentData<VesselMovement>(flagshipEntity).Velocity
                : float3.zero;
            var lead = velocity * leadSeconds;
            var leadClamped = math.lengthsq(lead) > 900f ? math.normalize(lead) * 30f : lead;

            if (Keyboard.current != null && Keyboard.current[snapToPlayerKey].wasPressedThisFrame)
            {
                SnapToPlayer(cam, flagshipPosition, leadClamped, distance);
                Debug.Log("[FleetcrawlUI] Camera snap-to-player.");
                return;
            }

            var desiredPosition = new Vector3(
                flagshipPosition.x + leadClamped.x * 0.35f,
                flagshipPosition.y + distance * heightRatio,
                flagshipPosition.z - distance + leadClamped.z * 0.35f);

            cam.transform.position = Vector3.Lerp(cam.transform.position, desiredPosition, Mathf.Clamp01(followLerp * Time.deltaTime));
            var lookTarget = new Vector3(flagshipPosition.x + leadClamped.x, flagshipPosition.y + 2f, flagshipPosition.z + leadClamped.z);
            var lookRotation = Quaternion.LookRotation((lookTarget - cam.transform.position).normalized, Vector3.up);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, lookRotation, Mathf.Clamp01(lookLerp * Time.deltaTime));
        }

        private void SnapToPlayer(Camera cam, float3 flagshipPosition, float3 lead, float distance)
        {
            var snapPosition = new Vector3(
                flagshipPosition.x + lead.x * 0.35f,
                flagshipPosition.y + distance * heightRatio,
                flagshipPosition.z - distance + lead.z * 0.35f);
            var lookTarget = new Vector3(flagshipPosition.x + lead.x, flagshipPosition.y + 2f, flagshipPosition.z + lead.z);
            cam.transform.position = snapPosition;
            cam.transform.rotation = Quaternion.LookRotation((lookTarget - snapPosition).normalized, Vector3.up);
        }

        private float ZoomDistanceFromT(float t)
        {
            if (maxDistance <= minDistance + 0.01f)
            {
                return minDistance;
            }

            return minDistance * math.pow(maxDistance / minDistance, math.saturate(t));
        }

        private float DistanceToZoomT(float distance)
        {
            var clampedDistance = math.clamp(distance, minDistance, maxDistance);
            if (maxDistance <= minDistance + 0.01f)
            {
                return 0f;
            }

            return math.saturate(math.log(clampedDistance / minDistance) / math.log(maxDistance / minDistance));
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
