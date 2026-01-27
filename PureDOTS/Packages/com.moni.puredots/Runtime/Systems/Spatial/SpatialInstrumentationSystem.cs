using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Systems.Spatial
{
    /// <summary>
    /// Emits console diagnostics for the spatial grid when instrumentation is enabled.
    /// </summary>
    [UpdateInGroup(typeof(global::PureDOTS.Systems.SpatialSystemGroup))]
    [UpdateAfter(typeof(SpatialGridBuildSystem))]
    public partial struct SpatialInstrumentationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<SpatialGridState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
            if (!SystemAPI.HasComponent<SpatialConsoleInstrumentation>(gridEntity))
            {
                return;
            }

            var instrumentation = SystemAPI.GetComponentRW<SpatialConsoleInstrumentation>(gridEntity);
            var settings = instrumentation.ValueRO;

            // Treat zero configuration as "disabled" to avoid spam unless explicitly requested.
            if (settings.MinTickDelta == 0 && settings.Flags == 0)
            {
                return;
            }

            var gridConfig = SystemAPI.GetSingleton<SpatialGridConfig>();
            var gridState = SystemAPI.GetSingleton<SpatialGridState>();

            if (settings.ShouldLogOnlyOnChange && settings.LastLoggedVersion == gridState.Version)
            {
                return;
            }

            var currentTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;
            if (settings.MinTickDelta > 0 && settings.LastLoggedTick != 0)
            {
                var ticksSinceLast = currentTick >= settings.LastLoggedTick
                    ? currentTick - settings.LastLoggedTick
                    : 0;
                if (ticksSinceLast < settings.MinTickDelta)
                {
                    return;
                }
            }

            var cellCount = math.max(gridConfig.CellCount, 0);
            float occupancy = cellCount > 0 ? (float)gridState.TotalEntries / cellCount : 0f;
            float occupancyRounded = math.round(occupancy * 1000f) * 0.001f;
            float rebuildRounded = math.round(math.max(gridState.LastRebuildMilliseconds, 0f) * 1000f) * 0.001f;
            var dirtyTotal = gridState.DirtyAddCount + gridState.DirtyUpdateCount + gridState.DirtyRemoveCount;
            string strategyLabel;
            switch (gridState.LastStrategy)
            {
                case SpatialGridRebuildStrategy.Full:
                    strategyLabel = "Full";
                    break;
                case SpatialGridRebuildStrategy.Partial:
                    strategyLabel = "Partial";
                    break;
                case SpatialGridRebuildStrategy.None:
                default:
                    strategyLabel = "None";
                    break;
            }

            UnityEngine.Debug.Log("[PureDOTS][Spatial] Cells=" + cellCount +
                      " Entries=" + gridState.TotalEntries +
                      " Version=" + gridState.Version +
                      " Tick=" + gridState.LastUpdateTick +
                      " Avg/Cell=" + occupancyRounded +
                      " Buffer=" + gridState.ActiveBufferIndex +
                      " Strategy=" + strategyLabel +
                      " Dirty(+/" + gridState.DirtyAddCount + ",~/" + gridState.DirtyUpdateCount + ",-/" + gridState.DirtyRemoveCount + ")=" + dirtyTotal +
                      " RebuildMs=" + rebuildRounded);

            instrumentation.ValueRW = new SpatialConsoleInstrumentation
            {
                MinTickDelta = settings.MinTickDelta,
                LastLoggedTick = currentTick,
                LastLoggedVersion = gridState.Version,
                Flags = settings.Flags
            };
        }
    }
}
