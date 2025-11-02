using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Builds and maintains a global registry of resource sites each frame (position/type/entity).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup), OrderFirst = true)]
    public partial struct ResourceRegistrySystem : ISystem
    {
        private EntityQuery _resourceQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _resourceQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceSourceConfig, ResourceSourceState, LocalTransform>()
                .Build();

            state.RequireForUpdate(_resourceQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            // Ensure registry singleton exists
            Entity registryEntity;
            if (!SystemAPI.TryGetSingletonEntity<ResourceRegistryTag>(out registryEntity))
            {
                registryEntity = em.CreateEntity();
                em.AddComponent<ResourceRegistryTag>(registryEntity);
                em.AddBuffer<ResourceRegistryEntry>(registryEntity);
            }

            var buffer = em.GetBuffer<ResourceRegistryEntry>(registryEntity);
            buffer.Clear();

            foreach (var (transform, typeId, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<ResourceTypeId>>()
                         .WithAll<ResourceSourceConfig, ResourceSourceState>()
                         .WithEntityAccess())
            {
                buffer.Add(new ResourceRegistryEntry
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    ResourceTypeId = typeId.ValueRO.Value
                });
            }
        }
    }
}




