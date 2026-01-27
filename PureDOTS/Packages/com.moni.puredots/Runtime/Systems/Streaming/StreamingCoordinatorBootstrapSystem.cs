using PureDOTS.Runtime.Streaming;
using Unity.Entities;

namespace PureDOTS.Systems.Streaming
{
    /// <summary>
    /// Ensures the streaming coordinator singleton and supporting components exist at startup.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct StreamingCoordinatorBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            EnsureCoordinator(ref state);
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // No-op: bootstrap runs once during initialization.
        }

        private void EnsureCoordinator(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            Entity coordinatorEntity;
            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<StreamingCoordinator>()))
            {
                if (query.IsEmptyIgnoreFilter)
                {
                    coordinatorEntity = entityManager.CreateEntity(typeof(StreamingCoordinator));
                    entityManager.SetComponentData(coordinatorEntity, new StreamingCoordinator
                    {
                        MaxConcurrentLoads = 2,
                        MaxLoadsPerTick = 1,
                        MaxUnloadsPerTick = 2,
                        CooldownTicks = 120,
                        WorldSequenceNumber = (uint)state.WorldUnmanaged.SequenceNumber
                    });
                    entityManager.AddBuffer<StreamingSectionCommand>(coordinatorEntity);
                    entityManager.AddComponentData(coordinatorEntity, new StreamingStatistics
                    {
                        FirstLoadTick = StreamingStatistics.TickUnset,
                        FirstUnloadTick = StreamingStatistics.TickUnset
                    });
                    entityManager.AddComponentData(coordinatorEntity, new StreamingDebugControl());
                }
                else
                {
                    coordinatorEntity = query.GetSingletonEntity();
                }
            }

            if (!entityManager.HasBuffer<StreamingSectionCommand>(coordinatorEntity))
            {
                entityManager.AddBuffer<StreamingSectionCommand>(coordinatorEntity);
            }

            if (!entityManager.HasComponent<StreamingStatistics>(coordinatorEntity))
            {
                entityManager.AddComponentData(coordinatorEntity, new StreamingStatistics
                {
                    FirstLoadTick = StreamingStatistics.TickUnset,
                    FirstUnloadTick = StreamingStatistics.TickUnset
                });
            }
            else
            {
                var stats = entityManager.GetComponentData<StreamingStatistics>(coordinatorEntity);
                if (stats.FirstLoadTick == 0)
                {
                    stats.FirstLoadTick = StreamingStatistics.TickUnset;
                }

                if (stats.FirstUnloadTick == 0)
                {
                    stats.FirstUnloadTick = StreamingStatistics.TickUnset;
                }

                entityManager.SetComponentData(coordinatorEntity, stats);
            }

            var coordinator = entityManager.GetComponentData<StreamingCoordinator>(coordinatorEntity);
            coordinator.WorldSequenceNumber = (uint)state.WorldUnmanaged.SequenceNumber;
            entityManager.SetComponentData(coordinatorEntity, coordinator);

            if (!entityManager.HasComponent<StreamingDebugControl>(coordinatorEntity))
            {
                entityManager.AddComponentData(coordinatorEntity, new StreamingDebugControl());
            }
        }
    }
}
