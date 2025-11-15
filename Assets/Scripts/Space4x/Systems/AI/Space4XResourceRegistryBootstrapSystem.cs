using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Ensures the shared PureDOTS resource registry entity exposes the component layout
    /// expected by the Space4X vessel AI systems (ResourceRegistry + ResourceRegistryEntry buffer).
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XResourceRegistryBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourceRegistryEntry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var registryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRegistryEntry>()
                .Build();

            if (registryQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var registryEntities = registryQuery.ToEntityArray(Allocator.Temp);
            var entityManager = state.EntityManager;

            foreach (var entity in registryEntities)
            {
                if (!entityManager.HasComponent<ResourceRegistry>(entity))
                {
                    entityManager.AddComponent<ResourceRegistry>(entity);
                }

                if (!entityManager.HasBuffer<ResourceRegistryEntry>(entity))
                {
                    entityManager.AddBuffer<ResourceRegistryEntry>(entity);
                }
            }

            registryEntities.Dispose();

            // Registry setup is a one-time operation; disable the system once complete.
            state.Enabled = false;
        }
    }
}
