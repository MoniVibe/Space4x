using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.AI.Reinforcement
{
    /// <summary>
    /// Static helpers for reinforcement arrival positioning.
    /// </summary>
    [BurstCompile]
    public static class ReinforcementHelpers
    {
        /// <summary>
        /// Calculates arrival position with scatter.
        /// </summary>
        public static float3 CalculateArrivalPosition(
            float3 rallyPoint,
            float3 facingDirection,
            in ArrivalPrecision precision,
            int unitIndex,
            int totalUnits)
        {
            // Use deterministic random based on seed + index
            uint hash = math.hash(new uint2(precision.Seed, (uint)unitIndex));
            var rng = new Random(hash);
            
            // Calculate scatter based on precision
            float scatter = precision.BaseScatter * (1f - precision.PrecisionModifier);
            scatter = math.clamp(scatter, 0, precision.MaxScatter);
            
            // Random angle
            float angle = rng.NextFloat(0, math.PI * 2f);
            
            // Random distance within scatter radius
            float distance = precision.MinDistance + rng.NextFloat(0, scatter);
            
            // Calculate offset
            float3 offset = new float3(
                math.cos(angle) * distance,
                0,
                math.sin(angle) * distance
            );
            
            return rallyPoint + offset;
        }

        /// <summary>
        /// Calculates formation-based arrival positions.
        /// </summary>
        public static void CalculateFormationPositions(
            float3 rallyPoint,
            float3 facingDirection,
            ArrivalFormation formation,
            float spacing,
            int unitCount,
            NativeArray<float3> positions)
        {
            float3 right = math.cross(facingDirection, new float3(0, 1, 0));
            float3 forward = facingDirection;
            
            switch (formation)
            {
                case ArrivalFormation.Circle:
                    CalculateCirclePositions(rallyPoint, spacing, unitCount, positions);
                    break;
                case ArrivalFormation.Line:
                    CalculateLinePositions(rallyPoint, right, spacing, unitCount, positions);
                    break;
                case ArrivalFormation.Wedge:
                    CalculateWedgePositions(rallyPoint, forward, right, spacing, unitCount, positions);
                    break;
                case ArrivalFormation.Flanking:
                    CalculateFlankingPositions(rallyPoint, right, spacing, unitCount, positions);
                    break;
                default:
                    // Scatter handled by CalculateArrivalPosition
                    for (int i = 0; i < unitCount && i < positions.Length; i++)
                    {
                        positions[i] = rallyPoint;
                    }
                    break;
            }
        }

        private static void CalculateCirclePositions(
            float3 center,
            float spacing,
            int count,
            NativeArray<float3> positions)
        {
            if (count <= 0) return;
            
            float radius = spacing * count / (2f * math.PI);
            radius = math.max(radius, spacing);
            
            for (int i = 0; i < count && i < positions.Length; i++)
            {
                float angle = (float)i / count * math.PI * 2f;
                positions[i] = center + new float3(
                    math.cos(angle) * radius,
                    0,
                    math.sin(angle) * radius
                );
            }
        }

        private static void CalculateLinePositions(
            float3 center,
            float3 lineDirection,
            float spacing,
            int count,
            NativeArray<float3> positions)
        {
            if (count <= 0) return;
            
            float halfWidth = (count - 1) * spacing * 0.5f;
            
            for (int i = 0; i < count && i < positions.Length; i++)
            {
                float offset = i * spacing - halfWidth;
                positions[i] = center + lineDirection * offset;
            }
        }

        private static void CalculateWedgePositions(
            float3 tip,
            float3 forward,
            float3 right,
            float spacing,
            int count,
            NativeArray<float3> positions)
        {
            if (count <= 0) return;
            
            // Leader at tip
            positions[0] = tip;
            
            int row = 1;
            int placed = 1;
            
            while (placed < count && placed < positions.Length)
            {
                int unitsInRow = row + 1;
                float rowBack = -row * spacing;
                float halfWidth = row * spacing * 0.5f;
                
                for (int i = 0; i < unitsInRow && placed < count && placed < positions.Length; i++)
                {
                    float sideOffset = i * spacing - halfWidth;
                    positions[placed] = tip + forward * rowBack + right * sideOffset;
                    placed++;
                }
                row++;
            }
        }

        private static void CalculateFlankingPositions(
            float3 center,
            float3 right,
            float spacing,
            int count,
            NativeArray<float3> positions)
        {
            if (count <= 0) return;
            
            int leftCount = count / 2;
            int rightCount = count - leftCount;
            
            float flankDistance = spacing * 3f; // Distance to flank
            
            // Left flank
            float3 leftCenter = center - right * flankDistance;
            for (int i = 0; i < leftCount && i < positions.Length; i++)
            {
                float offset = (i - leftCount / 2f) * spacing;
                positions[i] = leftCenter + new float3(0, 0, offset);
            }
            
            // Right flank
            float3 rightCenter = center + right * flankDistance;
            for (int i = 0; i < rightCount && leftCount + i < positions.Length; i++)
            {
                float offset = (i - rightCount / 2f) * spacing;
                positions[leftCount + i] = rightCenter + new float3(0, 0, offset);
            }
        }

        /// <summary>
        /// Calculates staggered arrival delay for a unit.
        /// </summary>
        public static float GetStaggeredDelay(int unitIndex, int totalUnits, in ArrivalTiming timing)
        {
            switch (timing.Pattern)
            {
                case ArrivalPattern.Simultaneous:
                    return timing.BaseDelay;
                    
                case ArrivalPattern.Staggered:
                    float interval = timing.WaveInterval / math.max(1, totalUnits - 1);
                    return timing.BaseDelay + unitIndex * interval;
                    
                case ArrivalPattern.Wave:
                    int waveSize = math.max(1, totalUnits / math.max(1, timing.WaveCount));
                    int waveIndex = unitIndex / waveSize;
                    return timing.BaseDelay + waveIndex * timing.WaveInterval;
                    
                case ArrivalPattern.Random:
                    var rng = new Random((uint)(timing.ScheduledTick + unitIndex));
                    return timing.BaseDelay + rng.NextFloat(-timing.DelayVariance, timing.DelayVariance);
                    
                default:
                    return timing.BaseDelay;
            }
        }

        /// <summary>
        /// Finds optimal rally point based on tactical situation.
        /// </summary>
        public static float3 FindOptimalRallyPoint(in RallyPointRequest request, float safetyMargin)
        {
            // Vector from enemy to objective
            float3 enemyToObjective = request.ObjectivePosition - request.EnemyCentroid;
            float3 friendlyToObjective = request.ObjectivePosition - request.FriendlyCentroid;
            
            float3 rallyPoint;
            
            if (request.AvoidEnemies != 0)
            {
                // Rally on opposite side from enemies
                float3 awayFromEnemy = math.normalizesafe(enemyToObjective);
                rallyPoint = request.ObjectivePosition + awayFromEnemy * request.PreferredDistance;
            }
            else if (request.FlankObjective != 0)
            {
                // Rally to the side (flanking position)
                float3 toEnemy = math.normalizesafe(-enemyToObjective);
                float3 perpendicular = math.cross(toEnemy, new float3(0, 1, 0));
                
                // Choose side closer to friendly forces
                float3 candidate1 = request.ObjectivePosition + perpendicular * request.PreferredDistance;
                float3 candidate2 = request.ObjectivePosition - perpendicular * request.PreferredDistance;
                
                float dist1 = math.distance(candidate1, request.FriendlyCentroid);
                float dist2 = math.distance(candidate2, request.FriendlyCentroid);
                
                rallyPoint = dist1 < dist2 ? candidate1 : candidate2;
            }
            else
            {
                // Rally between friendlies and objective
                float3 direction = math.normalizesafe(friendlyToObjective);
                rallyPoint = request.ObjectivePosition - direction * request.PreferredDistance;
            }
            
            return rallyPoint;
        }

        /// <summary>
        /// Checks if arrival group is complete.
        /// </summary>
        public static bool IsGroupComplete(in ArrivalGroup group)
        {
            return group.ArrivedCount >= group.GroupSize;
        }

        /// <summary>
        /// Calculates arrival tick for a unit.
        /// </summary>
        public static uint CalculateArrivalTick(uint currentTick, float delay, float ticksPerSecond)
        {
            return currentTick + (uint)(delay * ticksPerSecond);
        }

        /// <summary>
        /// Applies scatter to precision based on damage/emergency.
        /// </summary>
        public static ArrivalPrecision ApplyEmergencyScatter(in ArrivalPrecision basePrecision, float damageRatio)
        {
            // More damage = worse precision
            var result = basePrecision;
            result.PrecisionModifier *= (1f - damageRatio);
            result.BaseScatter *= (1f + damageRatio * 2f);
            return result;
        }
    }
}

