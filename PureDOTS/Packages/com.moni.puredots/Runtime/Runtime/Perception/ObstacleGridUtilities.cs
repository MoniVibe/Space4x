using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Perception
{
    /// <summary>
    /// Utilities for obstacle grid LOS checks (deterministic fallback for non-physics worlds).
    /// </summary>
    [BurstCompile]
    public static class ObstacleGridUtilities
    {
        /// <summary>
        /// Checks line-of-sight between two positions using obstacle grid.
        /// Uses Bresenham/DDA line stepping through grid cells.
        /// Returns true if LOS is clear, false if blocked.
        /// </summary>
        [BurstCompile]
        public static bool CheckLOS(
            in float3 start,
            in float3 end,
            in SpatialGridConfig gridConfig,
            in ObstacleGridConfig obstacleConfig,
            in DynamicBuffer<ObstacleGridCell> obstacleCells)
        {
            if (obstacleConfig.Enabled == 0)
            {
                return true; // Grid disabled, assume clear
            }

            if (obstacleCells.Length == 0)
            {
                return true; // No obstacle data, assume clear
            }

            SpatialHash.Quantize(start, gridConfig, out var startCell);
            SpatialHash.Quantize(end, gridConfig, out var endCell);

            // Clamp to grid bounds
            startCell = math.clamp(startCell, int3.zero, gridConfig.CellCounts - 1);
            endCell = math.clamp(endCell, int3.zero, gridConfig.CellCounts - 1);

            // If start and end are in same cell, check that cell
            if (math.all(startCell == endCell))
            {
                var cellId = SpatialHash.Flatten(in startCell, in gridConfig);
                if ((uint)cellId >= (uint)obstacleCells.Length)
                {
                    return true;
                }

                return obstacleCells[cellId].BlockingHeight < obstacleConfig.ObstacleThreshold;
            }

            // Use 3D DDA line stepping
            var delta = endCell - startCell;
            var absDelta = math.abs(delta);
            var step = math.sign(delta);
            var maxDelta = math.max(math.max(absDelta.x, absDelta.y), absDelta.z);

            if (maxDelta <= 0)
            {
                return true;
            }

            var current = startCell;
            var error = new int3(absDelta.x * 2, absDelta.y * 2, absDelta.z * 2);
            var deltaError = new int3(absDelta.x * 2, absDelta.y * 2, absDelta.z * 2);

            for (int i = 0; i <= maxDelta; i++)
            {
                // Check current cell
                var cellId = SpatialHash.Flatten(in current, in gridConfig);
                if ((uint)cellId >= (uint)obstacleCells.Length)
                {
                    return true; // Out of bounds, assume clear
                }

                var cell = obstacleCells[cellId];
                if (cell.BlockingHeight >= obstacleConfig.ObstacleThreshold)
                {
                    return false; // Blocked
                }

                // Step to next cell
                if (i == maxDelta)
                {
                    break; // Reached end
                }

                if (error.x >= maxDelta)
                {
                    current.x += step.x;
                    error.x -= maxDelta * 2;
                }

                if (error.y >= maxDelta)
                {
                    current.y += step.y;
                    error.y -= maxDelta * 2;
                }

                if (error.z >= maxDelta)
                {
                    current.z += step.z;
                    error.z -= maxDelta * 2;
                }

                error += deltaError;
            }

            return true; // LOS clear
        }
    }
}



