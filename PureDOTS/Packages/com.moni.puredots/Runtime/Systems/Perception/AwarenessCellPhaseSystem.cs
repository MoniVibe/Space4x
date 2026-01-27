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
    /// Processes sensor events per cell during phased aggregation.
    /// Collates per-thread work entries into deterministic snapshots.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(SensorEventEmitSystem))]
    public partial struct AwarenessCellPhaseSystem : ISystem
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

            if (!SystemAPI.HasBuffer<SensorCellEvent>(gridEntity))
            {
                return;
            }

            var eventsBuffer = SystemAPI.GetBuffer<SensorCellEvent>(gridEntity);
            if (eventsBuffer.Length == 0)
            {
                return;
            }

            // Aggregate events by cell
            // Use NativeHashMap for single-threaded aggregation (simpler for prototype)
            var cellWorkMap = new NativeHashMap<int, AwarenessWorkEntry>(1024, Allocator.TempJob);
            var eventsArray = eventsBuffer.AsNativeArray();

            // Aggregate events sequentially (deterministic)
            for (int i = 0; i < eventsArray.Length; i++)
            {
                var evt = eventsArray[i];
                var cellId = evt.CellId;

                if (!cellWorkMap.TryGetValue(cellId, out var work))
                {
                    work = new AwarenessWorkEntry
                    {
                        CellId = cellId,
                        EntityCount = 0,
                        HighestThreat = 0,
                        HighestThreatEntity = Entity.Null,
                        NearestDistance = float.MaxValue,
                        NearestEntity = Entity.Null,
                        MaxThreatChannels = PerceptionChannel.None,
                        MaxThreatPerChannel_Visual = 0,
                        MaxThreatPerChannel_Hearing = 0,
                        MaxThreatPerChannel_Smell = 0,
                        MaxThreatPerChannel_EM = 0,
                        MaxThreatPerChannel_Gravitic = 0,
                        MaxThreatPerChannel_Exotic = 0,
                        MaxThreatPerChannel_Paranormal = 0,
                        FactionEntityCount = 0,
                        HostileEntityCount = 0,
                        AllyEntityCount = 0,
                        NeutralEntityCount = 0
                    };
                }

                // Update work entry
                work.EntityCount++;
                if (evt.ThreatLevel > work.HighestThreat)
                {
                    work.HighestThreat = evt.ThreatLevel;
                    work.HighestThreatEntity = evt.DetectedEntity;
                }

                if (evt.Distance < work.NearestDistance)
                {
                    work.NearestDistance = evt.Distance;
                    work.NearestEntity = evt.DetectedEntity;
                }

                // Update per-channel threats
                if ((evt.DetectedChannels & PerceptionChannel.Vision) != 0)
                {
                    work.MaxThreatChannels |= PerceptionChannel.Vision;
                    if (evt.ThreatLevel > work.MaxThreatPerChannel_Visual)
                    {
                        work.MaxThreatPerChannel_Visual = evt.ThreatLevel;
                    }
                }

                if ((evt.DetectedChannels & PerceptionChannel.EM) != 0)
                {
                    work.MaxThreatChannels |= PerceptionChannel.EM;
                    if (evt.ThreatLevel > work.MaxThreatPerChannel_EM)
                    {
                        work.MaxThreatPerChannel_EM = evt.ThreatLevel;
                    }
                }

                // Update faction counts (simplified - assume threat > 0 is hostile)
                if (evt.ThreatLevel > 0)
                {
                    work.HostileEntityCount++;
                }
                else
                {
                    work.NeutralEntityCount++;
                }

                cellWorkMap[cellId] = work;
            }

            // Store aggregated work for merge phase
            // Use a singleton component to hold the work map temporarily
            if (!SystemAPI.HasSingleton<AwarenessWorkStorage>())
            {
                var storageEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<AwarenessWorkStorage>(storageEntity);
            }

            var workStorageEntity = SystemAPI.GetSingletonEntity<AwarenessWorkStorage>();
            if (!SystemAPI.HasBuffer<AwarenessWorkBuffer>(workStorageEntity))
            {
                state.EntityManager.AddBuffer<AwarenessWorkBuffer>(workStorageEntity);
            }

            var workBuffer = SystemAPI.GetBuffer<AwarenessWorkBuffer>(workStorageEntity);
            workBuffer.Clear();

            foreach (var kvp in cellWorkMap)
            {
                workBuffer.Add(new AwarenessWorkBuffer
                {
                    Work = kvp.Value
                });
            }

            cellWorkMap.Dispose();
        }
    }

    /// <summary>
    /// Temporary storage for awareness work entries between phases.
    /// </summary>
    public struct AwarenessWorkStorage : IComponentData { }

    /// <summary>
    /// Buffer element for awareness work entries.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AwarenessWorkBuffer : IBufferElementData
    {
        public AwarenessWorkEntry Work;
    }
}
