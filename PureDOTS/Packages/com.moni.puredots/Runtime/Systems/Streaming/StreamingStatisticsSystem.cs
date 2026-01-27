using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Streaming;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace PureDOTS.Systems.Streaming
{
    /// <summary>
    /// Aggregates streaming statistics for debug overlays and telemetry.
    /// </summary>
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    [UpdateAfter(typeof(StreamingStateSyncSystem))]
    public partial struct StreamingStatisticsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StreamingCoordinator>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var coordinatorEntity = SystemAPI.GetSingletonEntity<StreamingCoordinator>();
            var coordinator = SystemAPI.GetSingleton<StreamingCoordinator>();
            Assert.AreEqual(coordinator.WorldSequenceNumber, (uint)state.WorldUnmanaged.SequenceNumber,
                "[PureDOTS] StreamingCoordinator belongs to a different world.");

            var statsHandle = SystemAPI.GetComponentRW<StreamingStatistics>(coordinatorEntity);
            var stats = statsHandle.ValueRO;

            stats.DesiredCount = 0;
            stats.LoadedCount = 0;
            stats.LoadingCount = 0;
            stats.UnloadingCount = 0;
            stats.QueuedLoads = 0;
            stats.QueuedUnloads = 0;
            stats.PendingCommands = 0;
            stats.ActiveCooldowns = 0;

            uint currentTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;

            foreach (var sectionState in SystemAPI.Query<RefRO<StreamingSectionState>>())
            {
                var value = sectionState.ValueRO;
                switch (value.Status)
                {
                    case StreamingSectionStatus.Loaded:
                        stats.LoadedCount++;
                        stats.DesiredCount++;
                        break;
                    case StreamingSectionStatus.Loading:
                        stats.LoadingCount++;
                        stats.DesiredCount++;
                        break;
                    case StreamingSectionStatus.QueuedLoad:
                        stats.QueuedLoads++;
                        stats.DesiredCount++;
                        break;
                    case StreamingSectionStatus.QueuedUnload:
                        stats.QueuedUnloads++;
                        break;
                    case StreamingSectionStatus.Unloading:
                        stats.UnloadingCount++;
                        break;
                }

                if (value.CooldownUntilTick > currentTick)
                {
                    stats.ActiveCooldowns++;
                }
            }

            if (state.EntityManager.HasBuffer<StreamingSectionCommand>(coordinatorEntity))
            {
                int pending = state.EntityManager.GetBuffer<StreamingSectionCommand>(coordinatorEntity).Length;
                stats.PendingCommands = pending;
                if (pending > stats.PeakPendingCommands)
                {
                    stats.PeakPendingCommands = pending;
                }
            }

            statsHandle.ValueRW = stats;
        }
    }
}
