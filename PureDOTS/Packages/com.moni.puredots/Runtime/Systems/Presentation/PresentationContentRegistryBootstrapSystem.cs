using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PresentationContentRegistryBootstrapSystem : ISystem
    {
        private EntityQuery _registryQuery;

        public void OnCreate(ref SystemState state)
        {
            _registryQuery = state.GetEntityQuery(ComponentType.ReadOnly<PresentationContentRegistryReference>());
            EnsureRegistry(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            EnsureRegistry(ref state);
            DedupeRegistry(ref state);
        }

        private void EnsureRegistry(ref SystemState state)
        {
            if (_registryQuery.CalculateEntityCount() > 0)
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new PresentationContentRegistryReference
            {
                Registry = BlobAssetReference<PresentationContentRegistryBlob>.Null
            });
        }

        private void DedupeRegistry(ref SystemState state)
        {
            var count = _registryQuery.CalculateEntityCount();
            if (count <= 1)
            {
                return;
            }

            using var entities = _registryQuery.ToEntityArray(Allocator.Temp);
            var entityManager = state.EntityManager;
            Entity keep = Entity.Null;

            foreach (var entity in entities)
            {
                var registryRef = entityManager.GetComponentData<PresentationContentRegistryReference>(entity);
                if (registryRef.Registry.IsCreated)
                {
                    keep = entity;
                    break;
                }
            }

            if (keep == Entity.Null)
            {
                keep = entities[0];
            }

            foreach (var entity in entities)
            {
                if (entity == keep)
                {
                    continue;
                }

                entityManager.RemoveComponent<PresentationContentRegistryReference>(entity);
            }
        }
    }
}
