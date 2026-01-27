using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Formation;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Service for 3D formation positioning and combat advantages.
    /// </summary>
    [BurstCompile]
    public static class Formation3DService
    {
        /// <summary>
        /// Gets vertical offset for a formation member.
        /// Stacks members above/below leader based on index.
        /// </summary>
        public static float3 GetVerticalOffset(
            EntityStorageInfoLookup entityLookup,
            ComponentLookup<FormationState> formationLookup,
            Entity formation,
            int memberIndex)
        {
            if (!entityLookup.Exists(formation))
            {
                return float3.zero;
            }

            // Get formation state
            if (!formationLookup.HasComponent(formation))
            {
                return float3.zero;
            }

            var formationState = formationLookup[formation];
            float spacing = formationState.Spacing;

            // Calculate vertical offset based on member index
            // Even indices above, odd indices below
            float verticalOffset = 0f;
            if (memberIndex > 0)
            {
                int layer = (memberIndex - 1) / 2;
                bool isAbove = (memberIndex - 1) % 2 == 0;
                verticalOffset = (isAbove ? 1f : -1f) * spacing * (layer + 1);
            }

            return new float3(0f, verticalOffset, 0f);
        }

        /// <summary>
        /// Gets vertical engagement range for an entity.
        /// </summary>
        public static float GetVerticalEngagementRange(
            EntityStorageInfoLookup entityLookup,
            ComponentLookup<VerticalEngagementRange> rangeLookup,
            Entity entity)
        {
            if (!entityLookup.Exists(entity))
            {
                return 0f;
            }

            if (!rangeLookup.HasComponent(entity))
            {
                // Default vertical range (can be extended)
                return 100f;
            }

            var range = rangeLookup[entity];
            return range.VerticalRange;
        }

        /// <summary>
        /// Calculates 3D advantage multiplier based on relative positions.
        /// High ground and flanking provide bonuses.
        /// </summary>
        public static float Get3DAdvantageMultiplier(
            EntityStorageInfoLookup entityLookup,
            ComponentLookup<Advantage3D> advantageLookup,
            Entity attacker,
            float3 attackerPosition,
            Entity target,
            float3 targetPosition)
        {
            if (!entityLookup.Exists(attacker) || !entityLookup.Exists(target))
            {
                return 1f;
            }

            float3 delta = attackerPosition - targetPosition;
            float verticalDelta = delta.y;
            float horizontalDistance = math.length(new float2(delta.x, delta.z));

            float advantage = 1f;

            // High ground bonus (attacker above target)
            if (verticalDelta > 0f)
            {
                float highGroundBonus = math.clamp(verticalDelta / 50f, 0f, 0.3f); // Max 30% bonus
                advantage += highGroundBonus;
            }

            // Flanking bonus (attacker significantly above or below)
            if (math.abs(verticalDelta) > 20f && horizontalDistance < 30f)
            {
                float flankingBonus = math.clamp(math.abs(verticalDelta) / 100f, 0f, 0.2f); // Max 20% bonus
                advantage += flankingBonus;
            }

            // Update advantage component if present
            if (advantageLookup.HasComponent(attacker))
            {
                var adv = advantageLookup[attacker];
                adv.HighGroundBonus = verticalDelta > 0f ? math.clamp(verticalDelta / 50f, 0f, 0.3f) : 0f;
                adv.FlankingBonus = math.abs(verticalDelta) > 20f && horizontalDistance < 30f
                    ? math.clamp(math.abs(verticalDelta) / 100f, 0f, 0.2f)
                    : 0f;
                adv.VerticalAdvantage = advantage - 1f;
                advantageLookup[attacker] = adv;
            }

            return advantage;
        }

        /// <summary>
        /// Calculates target altitude for vertical movement.
        /// </summary>
        [BurstCompile]
        public static float CalculateTargetAltitude(
            in Entity entity,
            in Entity target,
            VerticalMovementMode mode,
            float baseAltitude)
        {
            float targetAltitude = baseAltitude;

            switch (mode)
            {
                case VerticalMovementMode.Ascend:
                    targetAltitude = baseAltitude + 20f;
                    break;
                case VerticalMovementMode.Descend:
                    targetAltitude = baseAltitude - 20f;
                    break;
                case VerticalMovementMode.Dive:
                    targetAltitude = baseAltitude - 50f;
                    break;
                case VerticalMovementMode.Climb:
                    targetAltitude = baseAltitude + 50f;
                    break;
            }

            return targetAltitude;
        }
    }
}

