using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Presentation.Overlay
{
    /// <summary>
    /// Carrier picker that works without colliders by testing distance to camera ray.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XEntityPicker : MonoBehaviour
    {
        [SerializeField] private bool _enabledInPlay = true;
        [SerializeField] private float _pickDistanceThreshold = 35f;

        private static Entity s_selectedCarrier = Entity.Null;

        public static bool TryGetSelectedCarrier(out Entity carrierEntity)
        {
            carrierEntity = s_selectedCarrier;
            return carrierEntity != Entity.Null;
        }

        public static void ClearSelection()
        {
            s_selectedCarrier = Entity.Null;
        }

        private void Update()
        {
            if (!_enabledInPlay || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                return;
            }

            if (!TryGetEntityManager(out var entityManager))
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            SelectNearestCarrier(entityManager, ray);
        }

        private void SelectNearestCarrier(EntityManager entityManager, Ray ray)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Carrier>(),
                ComponentType.ReadOnly<LocalTransform>());
            if (query.IsEmptyIgnoreFilter)
            {
                s_selectedCarrier = Entity.Null;
                return;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var bestEntity = Entity.Null;
            var bestDistance = float.PositiveInfinity;
            var bestAlongRay = float.PositiveInfinity;

            for (var i = 0; i < entities.Length; i++)
            {
                var position = (Vector3)transforms[i].Position;
                var alongRay = Vector3.Dot(position - ray.origin, ray.direction);
                if (alongRay < 0f)
                {
                    continue;
                }

                var closest = ray.origin + (ray.direction * alongRay);
                var distance = Vector3.Distance(position, closest);
                if (distance > _pickDistanceThreshold)
                {
                    continue;
                }

                var isCloser = distance < bestDistance;
                var isTieBreak = Mathf.Approximately(distance, bestDistance) && alongRay < bestAlongRay;
                if (!isCloser && !isTieBreak)
                {
                    continue;
                }

                bestEntity = entities[i];
                bestDistance = distance;
                bestAlongRay = alongRay;
            }

            s_selectedCarrier = bestEntity;
        }

        private static bool TryGetEntityManager(out EntityManager entityManager)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                return false;
            }

            entityManager = world.EntityManager;
            return true;
        }
    }
}
