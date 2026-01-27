using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace PureDOTS.Systems.Perception
{
    /// <summary>
    /// Merges per-thread awareness work entries into deterministic snapshots.
    /// Scans cells in index order to guarantee deterministic output.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(AwarenessCellPhaseSystem))]
    public partial struct AwarenessSnapshotMergeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<SpatialGridConfig>();
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

            var gridConfig = SystemAPI.GetSingleton<SpatialGridConfig>();
            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();

            if (!SystemAPI.HasSingleton<AwarenessWorkStorage>())
            {
                return;
            }

            var workStorageEntity = SystemAPI.GetSingletonEntity<AwarenessWorkStorage>();
            if (!SystemAPI.HasBuffer<AwarenessWorkBuffer>(workStorageEntity))
            {
                return;
            }

            var workBuffer = SystemAPI.GetBuffer<AwarenessWorkBuffer>(workStorageEntity);
            if (workBuffer.Length == 0)
            {
                return;
            }

            // Sort work entries by cell ID for deterministic processing
            var sortedWork = new NativeList<AwarenessWorkEntry>(workBuffer.Length, Allocator.TempJob);
            for (int i = 0; i < workBuffer.Length; i++)
            {
                sortedWork.Add(workBuffer[i].Work);
            }

            sortedWork.Sort(new CellIdComparer());

            // Ensure snapshot buffers exist on grid entity
            if (!SystemAPI.HasBuffer<AwarenessCellSnapshotBuffer>(gridEntity))
            {
                state.EntityManager.AddBuffer<AwarenessCellSnapshotBuffer>(gridEntity);
            }

            if (!SystemAPI.HasBuffer<ThreatSnapshotBuffer>(gridEntity))
            {
                state.EntityManager.AddBuffer<ThreatSnapshotBuffer>(gridEntity);
            }

            var awarenessSnapshots = SystemAPI.GetBuffer<AwarenessCellSnapshotBuffer>(gridEntity);
            var threatSnapshots = SystemAPI.GetBuffer<ThreatSnapshotBuffer>(gridEntity);

            // Clear and rebuild snapshots
            awarenessSnapshots.Clear();
            threatSnapshots.Clear();

            var snapshotVersion = timeState.Tick;

            for (int i = 0; i < sortedWork.Length; i++)
            {
                var work = sortedWork[i];

                // Create awareness snapshot
                awarenessSnapshots.Add(new AwarenessCellSnapshotBuffer
                {
                    Snapshot = new AwarenessCellSnapshot
                    {
                        CellId = work.CellId,
                        EntityCount = work.EntityCount,
                        HighestThreat = work.HighestThreat,
                        HighestThreatEntity = work.HighestThreatEntity,
                        NearestDistance = work.NearestDistance,
                        NearestEntity = work.NearestEntity,
                        FactionEntityCount = work.FactionEntityCount,
                        HostileEntityCount = work.HostileEntityCount,
                        AllyEntityCount = work.AllyEntityCount,
                        NeutralEntityCount = work.NeutralEntityCount,
                        LastUpdateTick = timeState.Tick,
                        Version = snapshotVersion
                    }
                });

                // Create threat snapshot
                threatSnapshots.Add(new ThreatSnapshotBuffer
                {
                    Snapshot = new ThreatSnapshot
                    {
                        CellId = work.CellId,
                        MaxThreat_Visual = work.MaxThreatPerChannel_Visual,
                        MaxThreat_Hearing = work.MaxThreatPerChannel_Hearing,
                        MaxThreat_Smell = work.MaxThreatPerChannel_Smell,
                        MaxThreat_EM = work.MaxThreatPerChannel_EM,
                        MaxThreat_Gravitic = work.MaxThreatPerChannel_Gravitic,
                        MaxThreat_Exotic = work.MaxThreatPerChannel_Exotic,
                        MaxThreat_Paranormal = work.MaxThreatPerChannel_Paranormal,
                        ThreatChannels = work.MaxThreatChannels,
                        LastUpdateTick = timeState.Tick,
                        Version = snapshotVersion
                    }
                });
            }

            sortedWork.Dispose();

            if (!SystemAPI.TryGetSingletonRW<SensorCommsScalingTelemetry>(out var telemetry))
            {
                var telemetryEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<SensorCommsScalingTelemetry>(telemetryEntity);
                telemetry = SystemAPI.GetSingletonRW<SensorCommsScalingTelemetry>();
            }

            telemetry.ValueRW.CellsProcessedThisTick = awarenessSnapshots.Length;
            telemetry.ValueRW.EventsAggregatedThisTick = workBuffer.Length;
            telemetry.ValueRW.LastResetTick = timeState.Tick;
        }

        private struct CellIdComparer : System.Collections.Generic.IComparer<AwarenessWorkEntry>
        {
            public int Compare(AwarenessWorkEntry x, AwarenessWorkEntry y)
            {
                return x.CellId.CompareTo(y.CellId);
            }
        }
    }

    /// <summary>
    /// Buffer element for awareness snapshots.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AwarenessCellSnapshotBuffer : IBufferElementData
    {
        public AwarenessCellSnapshot Snapshot;
    }

    /// <summary>
    /// Buffer element for threat snapshots.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ThreatSnapshotBuffer : IBufferElementData
    {
        public ThreatSnapshot Snapshot;
    }
}
