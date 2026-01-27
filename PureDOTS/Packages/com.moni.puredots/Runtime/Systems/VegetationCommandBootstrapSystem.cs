using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Ensures the vegetation harvest command queue exists for simulation systems.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct VegetationCommandBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            Entity harvestQueueEntity;
            using (var harvestQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VegetationHarvestCommandQueue>()))
            {
                if (harvestQuery.IsEmptyIgnoreFilter)
                {
                    harvestQueueEntity = entityManager.CreateEntity(typeof(VegetationHarvestCommandQueue));
                }
                else
                {
                    harvestQueueEntity = harvestQuery.GetSingletonEntity();
                }
            }

            if (!entityManager.HasBuffer<VegetationHarvestCommand>(harvestQueueEntity))
            {
                entityManager.AddBuffer<VegetationHarvestCommand>(harvestQueueEntity);
            }

            if (!entityManager.HasBuffer<VegetationHarvestReceipt>(harvestQueueEntity))
            {
                entityManager.AddBuffer<VegetationHarvestReceipt>(harvestQueueEntity);
            }

            using (var spawnQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VegetationSpawnCommandQueue>()))
            {
                if (spawnQuery.IsEmptyIgnoreFilter)
                {
                    var spawnQueueEntity = entityManager.CreateEntity(typeof(VegetationSpawnCommandQueue));
                    entityManager.AddBuffer<VegetationSpawnCommand>(spawnQueueEntity);
                }
                else
                {
                    var spawnQueueEntity = spawnQuery.GetSingletonEntity();
                    if (!entityManager.HasBuffer<VegetationSpawnCommand>(spawnQueueEntity))
                    {
                        entityManager.AddBuffer<VegetationSpawnCommand>(spawnQueueEntity);
                    }
                }
            }

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // No-op. Bootstrap only.
        }
    }
}
