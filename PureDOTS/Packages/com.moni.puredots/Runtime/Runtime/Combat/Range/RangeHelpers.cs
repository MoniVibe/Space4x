using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat.Range
{
    /// <summary>
    /// Static helpers for range calculations.
    /// Pure functions for Burst-compatible range checks.
    /// </summary>
    public static class RangeHelpers
    {
        /// <summary>
        /// Checks if target is within melee range.
        /// </summary>
        public static bool InMeleeRange(float3 attacker, float3 target, float meleeRange)
        {
            float distSq = math.distancesq(attacker, target);
            return distSq <= meleeRange * meleeRange;
        }

        /// <summary>
        /// Checks if target is within ranged attack range.
        /// </summary>
        public static bool InRangedRange(float3 attacker, float3 target, float minRange, float maxRange)
        {
            float distSq = math.distancesq(attacker, target);
            float minSq = minRange * minRange;
            float maxSq = maxRange * maxRange;
            return distSq >= minSq && distSq <= maxSq;
        }

        /// <summary>
        /// Checks if target is within any attack range.
        /// </summary>
        public static bool InAnyRange(float3 attacker, float3 target, in CombatRange range)
        {
            float dist = math.distance(attacker, target);

            if (range.CanMelee && dist <= range.MeleeRange)
            {
                return true;
            }

            if (range.CanRanged && dist >= range.RangedMinRange && dist <= range.RangedMaxRange)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets detailed range check result.
        /// </summary>
        public static RangeCheckResult CheckRange(float3 attacker, float3 target, in CombatRange range)
        {
            float dist = math.distance(attacker, target);

            var result = new RangeCheckResult
            {
                Distance = dist,
                InRange = false,
                TooClose = false,
                TooFar = false,
                BestAttackType = AttackRangeType.Melee
            };

            // Check melee range
            if (range.CanMelee && dist <= range.MeleeRange)
            {
                result.InRange = true;
                result.BestAttackType = AttackRangeType.Melee;
                return result;
            }

            // Check ranged range
            if (range.CanRanged)
            {
                if (dist < range.RangedMinRange)
                {
                    result.TooClose = true;
                    // Can still melee if available
                    if (range.CanMelee && dist <= range.MeleeRange)
                    {
                        result.InRange = true;
                        result.BestAttackType = AttackRangeType.Melee;
                    }
                }
                else if (dist <= range.RangedMaxRange)
                {
                    result.InRange = true;
                    result.BestAttackType = AttackRangeType.Ranged;
                }
                else
                {
                    result.TooFar = true;
                }
            }
            else if (range.CanMelee)
            {
                // Melee only - check if too far
                if (dist > range.MeleeRange)
                {
                    result.TooFar = true;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the best attack type for current distance.
        /// </summary>
        public static AttackRangeType GetBestAttackType(float distance, in CombatRange range)
        {
            // Prefer melee if in range
            if (range.CanMelee && distance <= range.MeleeRange)
            {
                return AttackRangeType.Melee;
            }

            // Use ranged if available and in range
            if (range.CanRanged && distance >= range.RangedMinRange && distance <= range.RangedMaxRange)
            {
                return AttackRangeType.Ranged;
            }

            // Default to melee
            return AttackRangeType.Melee;
        }

        /// <summary>
        /// Calculates distance to move to reach preferred range.
        /// Positive = move closer, negative = move away.
        /// </summary>
        public static float GetDistanceToPreferredRange(float currentDistance, in CombatRange range)
        {
            float preferredRange = range.PreferredRange > 0 ? range.PreferredRange : range.MeleeRange;

            return currentDistance - preferredRange;
        }

        /// <summary>
        /// Checks if position is within AOE radius of center.
        /// </summary>
        public static bool InAOERange(float3 position, float3 center, float aoeRadius)
        {
            float distSq = math.distancesq(position, center);
            return distSq <= aoeRadius * aoeRadius;
        }

        /// <summary>
        /// Calculates falloff damage based on distance from AOE center.
        /// Returns 1.0 at center, decreasing to 0.0 at edge.
        /// </summary>
        public static float GetAOEFalloff(float3 position, float3 center, float aoeRadius, float falloffPower = 1f)
        {
            float dist = math.distance(position, center);
            if (dist >= aoeRadius)
            {
                return 0f;
            }

            float normalizedDist = dist / aoeRadius;
            return math.pow(1f - normalizedDist, falloffPower);
        }

        /// <summary>
        /// Gets direction from attacker to target.
        /// </summary>
        public static float3 GetDirectionToTarget(float3 attacker, float3 target)
        {
            float3 diff = target - attacker;
            float len = math.length(diff);
            return len > 1e-6f ? diff / len : float3.zero;
        }

        /// <summary>
        /// Calculates optimal position for ranged attack.
        /// Returns position at preferred range from target.
        /// </summary>
        public static float3 GetOptimalRangedPosition(float3 attacker, float3 target, in CombatRange range)
        {
            float preferredRange = range.PreferredRange > 0
                ? range.PreferredRange
                : (range.RangedMinRange + range.RangedMaxRange) * 0.5f;

            float3 direction = GetDirectionToTarget(target, attacker); // Direction away from target
            return target + direction * preferredRange;
        }

        /// <summary>
        /// Checks if line of sight is blocked (simple 2D check).
        /// Games should implement proper LOS with raycasts.
        /// </summary>
        public static bool HasLineOfSight(float3 attacker, float3 target)
        {
            // Placeholder - games should implement proper LOS checks
            // This just checks if target is not directly below/above
            float verticalDiff = math.abs(attacker.y - target.y);
            float horizontalDist = math.distance(
                new float2(attacker.x, attacker.z),
                new float2(target.x, target.z));

            // Simple check - if vertical difference is more than horizontal, LOS may be blocked
            return horizontalDist > verticalDiff * 0.5f;
        }
    }
}

