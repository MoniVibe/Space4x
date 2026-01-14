using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems.Perception;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Perception
{
    /// <summary>
    /// Consumes awareness snapshots for Space4X alert state and knowledge systems.
    /// Feature-flag guarded so production worlds can keep legacy flow until ready.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.PerceptionSystemGroup))]
    [UpdateAfter(typeof(AwarenessSnapshotMergeSystem))]
    public partial struct Space4XKnowledgeMergeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<SimulationFeatureFlags>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.SensorCommsScalingPrototype) == 0)
            {
                return;
            }

            if (!SystemAPI.HasSingleton<SpatialGridConfig>())
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();

            if (!SystemAPI.HasBuffer<AwarenessSnapshotBuffer>(gridEntity))
            {
                return;
            }

            var snapshots = SystemAPI.GetBuffer<AwarenessSnapshotBuffer>(gridEntity);
            if (snapshots.Length == 0)
            {
                return;
            }

            // Aggregate threat levels per faction (simplified prototype)
            // In full implementation, this would update AlertStateComponent or telemetry counters
            var maxThreat = (byte)0;
            var totalHostileEntities = 0;

            for (int i = 0; i < snapshots.Length; i++)
            {
                var snapshot = snapshots[i].Snapshot;
                if (snapshot.HighestThreat > maxThreat)
                {
                    maxThreat = snapshot.HighestThreat;
                }

                totalHostileEntities += snapshot.HostileEntityCount;
            }

            // TODO: Update AlertStateComponent or faction knowledge systems
            // For now, this is a placeholder that proves the dataflow works
            // without scanning all entities
        }
    }
}
