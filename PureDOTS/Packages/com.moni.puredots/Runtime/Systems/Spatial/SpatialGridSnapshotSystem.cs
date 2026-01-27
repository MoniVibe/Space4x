using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Spatial
{
    /// <summary>
    /// Captures and restores spatial grid snapshots for rewind/replay validation.
    /// Snapshots are stored as components on the grid singleton entity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    public partial struct SpatialGridSnapshotSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var captureTick = timeState.Tick;
            if (SystemAPI.TryGetSingleton<TimeContext>(out var timeContext))
            {
                captureTick = timeContext.ViewTick;
            }
            // Only capture snapshots during Record mode
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<SpatialGridConfig>(out var gridEntity))
            {
                return;
            }

            // Capture snapshot of current state
            if (SystemAPI.HasComponent<SpatialGridState>(gridEntity))
            {
                var gridState = SystemAPI.GetComponent<SpatialGridState>(gridEntity);
                var snapshot = SpatialGridSnapshot.FromState(gridState, captureTick);

                // Store snapshot as component (replacing previous)
                if (SystemAPI.HasComponent<SpatialGridSnapshot>(gridEntity))
                {
                    SystemAPI.SetComponent(gridEntity, snapshot);
                }
                else
                {
                    state.EntityManager.AddComponentData(gridEntity, snapshot);
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

