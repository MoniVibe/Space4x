using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace PureDOTS.Systems.Perception
{
    /// <summary>
    /// Assigns deterministic colors to spatial grid cells for phased updates.
    /// Uses Morton key bits to ensure deterministic coloring.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(Spatial.SpatialGridBuildSystem))]
    public partial struct SensorCellColoringSystem : ISystem
    {
        private const byte DefaultColorCount = 4;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<SpatialGridState>();
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

            if (!SystemAPI.HasSingleton<SpatialGridConfig>() || !SystemAPI.HasSingleton<SpatialGridState>())
            {
                return;
            }

            var gridConfig = SystemAPI.GetSingleton<SpatialGridConfig>();
            var gridState = SystemAPI.GetSingleton<SpatialGridState>();
            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();

            // Ensure coloring state exists
            if (!SystemAPI.HasComponent<SensorCellColoringState>(gridEntity))
            {
                state.EntityManager.AddComponent<SensorCellColoringState>(gridEntity);
            }

            var coloringState = SystemAPI.GetComponentRW<SensorCellColoringState>(gridEntity);

            // Update coloring if grid version changed
            if (coloringState.ValueRO.Version < gridState.Version)
            {
                UpdateCellColoring(ref state, gridConfig, gridState, ref coloringState.ValueRW);
            }

            // Rotate current color for phased processing
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentColor = (byte)(timeState.Tick % DefaultColorCount);
            coloringState.ValueRW.CurrentColor = currentColor;
            coloringState.ValueRW.LastUpdateTick = timeState.Tick;
        }

        [BurstCompile]
        private void UpdateCellColoring(
            ref SystemState state,
            in SpatialGridConfig config,
            in SpatialGridState gridState,
            ref SensorCellColoringState coloringState)
        {
            coloringState.ColorCount = DefaultColorCount;
            coloringState.Version = gridState.Version;

            // Update SensorCellIndex components for entities with SpatialGridResidency
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var job = new UpdateCellColoringJob
            {
                Config = config,
                GridStateVersion = gridState.Version,
                Ecb = ecb.AsParallelWriter()
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private partial struct UpdateCellColoringJob : IJobEntity
        {
            public SpatialGridConfig Config;
            public uint GridStateVersion;
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute([EntityIndexInQuery] int entityInQueryIndex, Entity entity, ref SpatialGridResidency residency)
            {
                if (residency.Version != GridStateVersion)
                {
                    return;
                }

                // Compute Morton key for deterministic coloring
                SpatialHash.Unflatten(residency.CellId, Config, out var cellCoords);
                var mortonKey = SpatialHash.MortonKey(cellCoords, Config.HashSeed);
                var color = (byte)(mortonKey & 0x3); // 4-color map

                // Add or update SensorCellIndex
                Ecb.AddComponent(entityInQueryIndex, entity, new SensorCellIndex
                {
                    CellId = residency.CellId,
                    Color = color,
                    Version = GridStateVersion,
                    MortonKey = mortonKey
                });
            }
        }
    }
}
