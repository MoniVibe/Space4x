using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Perception
{
    /// <summary>
    /// Populates obstacle grid from static geometry entities.
    /// Runs once at startup or on-demand when ObstacleGridRebuildRequest is present.
    /// Can be disabled if world doesn't need deterministic LOS fallback.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup), OrderFirst = true)]
    public partial struct ObstacleGridBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<SpatialGridConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.PerceptionEnabled) == 0)
            {
                return;
            }

            if (!SystemAPI.HasSingleton<SpatialGridConfig>())
            {
                return;
            }

            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
            if (!SystemAPI.HasComponent<ObstacleGridConfig>(gridEntity))
            {
                return; // Obstacle grid not configured
            }

            var obstacleConfig = SystemAPI.GetComponentRO<ObstacleGridConfig>(gridEntity).ValueRO;
            if (obstacleConfig.Enabled == 0)
            {
                return; // Obstacle grid disabled
            }

            // Check if rebuild requested or first run
            var timeState = SystemAPI.GetSingleton<TimeState>();
            bool shouldRebuild = false;

            if (SystemAPI.HasComponent<ObstacleGridRebuildRequest>(gridEntity))
            {
                shouldRebuild = true;
                state.EntityManager.RemoveComponent<ObstacleGridRebuildRequest>(gridEntity);
            }
            else if (!SystemAPI.HasBuffer<ObstacleGridCell>(gridEntity))
            {
                shouldRebuild = true; // First run, need to create buffer
            }

            if (!shouldRebuild)
            {
                return;
            }

            var gridConfig = SystemAPI.GetSingleton<SpatialGridConfig>();

            // Ensure obstacle cell buffer exists
            if (!SystemAPI.HasBuffer<ObstacleGridCell>(gridEntity))
            {
                var newObstacleCells = state.EntityManager.AddBuffer<ObstacleGridCell>(gridEntity);
                newObstacleCells.ResizeUninitialized(gridConfig.CellCount);
                for (int i = 0; i < gridConfig.CellCount; i++)
                {
                    newObstacleCells[i] = new ObstacleGridCell
                    {
                        BlockingHeight = 0f,
                        LastUpdatedTick = timeState.Tick
                    };
                }
            }

            var obstacleCells = SystemAPI.GetBuffer<ObstacleGridCell>(gridEntity);

            // Clear all cells
            for (int i = 0; i < obstacleCells.Length; i++)
            {
                var cell = obstacleCells[i];
                cell.BlockingHeight = 0f;
                cell.LastUpdatedTick = timeState.Tick;
                obstacleCells[i] = cell;
            }

            // Query obstacle entities
            var obstacleQuery = SystemAPI.QueryBuilder()
                .WithAll<ObstacleTag, LocalTransform>()
                .Build();

            if (obstacleQuery.IsEmptyIgnoreFilter)
            {
                return; // No obstacles to process
            }

            // Process each obstacle entity
            foreach (var (transform, obstacleEntity) in SystemAPI.Query<RefRO<LocalTransform>>()
                .WithAll<ObstacleTag>()
                .WithEntityAccess())
            {
                var position = transform.ValueRO.Position;
                float obstacleHeight = 1f; // Default height

                // Check for explicit height override
                if (SystemAPI.HasComponent<ObstacleHeight>(obstacleEntity))
                {
                    obstacleHeight = SystemAPI.GetComponentRO<ObstacleHeight>(obstacleEntity).ValueRO.Height;
                }

                if (obstacleHeight <= 0f)
                {
                    continue; // Skip zero-height obstacles
                }

                // Quantize position to grid cells
                SpatialHash.Quantize(position, gridConfig, out var cellCoords);

                // Update cell(s) affected by obstacle
                // For simplicity, update the cell containing the obstacle position
                // Future: could expand to multiple cells based on obstacle size
                var cellId = SpatialHash.Flatten(in cellCoords, in gridConfig);
                if ((uint)cellId < (uint)obstacleCells.Length)
                {
                    var cell = obstacleCells[cellId];
                    // Use maximum height if multiple obstacles overlap
                    cell.BlockingHeight = math.max(cell.BlockingHeight, obstacleHeight);
                    cell.LastUpdatedTick = timeState.Tick;
                    obstacleCells[cellId] = cell;
                }
            }
        }
    }
}



