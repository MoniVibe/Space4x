using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace PureDOTS.Runtime.Spatial
{
    /// <summary>
    /// Static helpers for spatial modifiers and zone calculations.
    /// </summary>
    [BurstCompile]
    public static class SpatialModifierHelpers
    {
        /// <summary>
        /// Checks if position is within zone.
        /// </summary>
        public static bool IsInZone(
            float3 position,
            in SpatialZone zone)
        {
            float3 offset = position - zone.Center;
            
            return zone.ZoneShape switch
            {
                0 => math.length(offset) <= zone.Radius, // Sphere
                1 => math.length(offset.xz) <= zone.Radius && 
                     math.abs(offset.y) <= zone.Height * 0.5f, // Cylinder
                2 => math.abs(offset.x) <= zone.Radius && 
                     math.abs(offset.y) <= zone.Height * 0.5f && 
                     math.abs(offset.z) <= zone.Radius, // Box
                _ => false
            };
        }

        /// <summary>
        /// Calculates falloff factor based on distance.
        /// </summary>
        public static float CalculateFalloff(
            float3 position,
            in SpatialZone zone,
            float falloffCurve)
        {
            float distance = math.length(position - zone.Center);
            
            if (distance <= zone.FalloffStart)
                return 1f;
            
            if (distance >= zone.Radius)
                return 0f;
            
            float falloffRange = zone.Radius - zone.FalloffStart;
            float falloffProgress = (distance - zone.FalloffStart) / falloffRange;
            
            // Apply curve (1 = linear, 2 = quadratic, etc.)
            return math.pow(1f - falloffProgress, falloffCurve);
        }

        /// <summary>
        /// Creates default accumulated modifiers.
        /// </summary>
        public static AccumulatedSpatialModifiers CreateDefaultModifiers()
        {
            return new AccumulatedSpatialModifiers
            {
                MovementMod = 1f,
                VisibilityMod = 1f,
                SensorMod = 1f,
                DamageMod = 1f,
                TimeFlowMod = 1f,
                YieldMod = 1f,
                InHazard = 0
            };
        }

        /// <summary>
        /// Applies a single modifier to accumulated modifiers.
        /// </summary>
        public static void ApplyModifier(
            ref AccumulatedSpatialModifiers result,
            SpatialModifierType type,
            float value,
            bool isMultiplier)
        {
            switch (type)
            {
                case SpatialModifierType.MovementSpeed:
                case SpatialModifierType.MovementCost:
                    if (isMultiplier) result.MovementMod *= value;
                    else result.MovementMod += value;
                    break;
                case SpatialModifierType.Visibility:
                    if (isMultiplier) result.VisibilityMod *= value;
                    else result.VisibilityMod += value;
                    break;
                case SpatialModifierType.SensorRange:
                    if (isMultiplier) result.SensorMod *= value;
                    else result.SensorMod += value;
                    break;
                case SpatialModifierType.DamageModifier:
                    if (isMultiplier) result.DamageMod *= value;
                    else result.DamageMod += value;
                    break;
                case SpatialModifierType.TimeFlowRate:
                    if (isMultiplier) result.TimeFlowMod *= value;
                    else result.TimeFlowMod += value;
                    break;
                case SpatialModifierType.YieldMultiplier:
                    if (isMultiplier) result.YieldMod *= value;
                    else result.YieldMod += value;
                    break;
                case SpatialModifierType.PeriodicDamage:
                    result.InHazard = 1;
                    break;
            }
        }

        /// <summary>
        /// Accumulates modifiers from a single zone.
        /// </summary>
        public static void AccumulateFromZone(
            ref AccumulatedSpatialModifiers result,
            float3 position,
            in SpatialZone zone,
            in DynamicBuffer<ZoneModifier> modifiers)
        {
            if (zone.IsActive == 0) return;
            if (!IsInZone(position, zone)) return;

            for (int j = 0; j < modifiers.Length; j++)
            {
                float falloff = CalculateFalloff(position, zone, modifiers[j].FalloffCurve);
                float effectValue = modifiers[j].Value * falloff;

                ApplyModifier(ref result, modifiers[j].Type, effectValue, modifiers[j].IsMultiplier != 0);
            }
        }

        /// <summary>
        /// Calculates movement cost through zone.
        /// </summary>
        public static float CalculateMovementCost(
            float3 start,
            float3 end,
            in AccumulatedSpatialModifiers startMods,
            in AccumulatedSpatialModifiers endMods)
        {
            float distance = math.length(end - start);
            float avgMovementMod = (startMods.MovementMod + endMods.MovementMod) * 0.5f;
            
            // Movement mod < 1 = slower, > 1 = faster
            return distance / math.max(0.1f, avgMovementMod);
        }

        /// <summary>
        /// Calculates hazard damage this tick.
        /// </summary>
        public static float CalculateHazardDamage(
            in HazardZone hazard,
            float resistance,
            uint currentTick)
        {
            if (currentTick - hazard.LastDamageTick < hazard.DamageInterval)
                return 0;
            
            // Resistance reduces damage
            float damage = hazard.DamagePerTick * (1f - resistance);
            return math.max(0, damage);
        }

        /// <summary>
        /// Calculates time flow for region.
        /// </summary>
        public static float GetEffectiveTimeFlow(
            in TimeFlowRegion region,
            uint currentTick)
        {
            // Check for temporal anomaly
            bool inAnomaly = (currentTick - region.LastAnomalyTick) < 100;
            
            if (inAnomaly)
            {
                // Anomaly causes instability
                return region.TimeMultiplier * (0.5f + region.StabilityFactor * 0.5f);
            }
            
            return region.TimeMultiplier;
        }

        /// <summary>
        /// Applies weather penalty to modifiers.
        /// </summary>
        public static void ApplyWeather(
            ref AccumulatedSpatialModifiers mods,
            in WeatherCondition weather)
        {
            mods.MovementMod *= 1f - (weather.MovementPenalty * weather.Intensity);
            mods.VisibilityMod *= 1f - (weather.VisibilityPenalty * weather.Intensity);
        }

        /// <summary>
        /// Gets effective visibility range.
        /// </summary>
        public static float GetEffectiveVisibility(
            float baseVisibility,
            in AccumulatedSpatialModifiers mods)
        {
            return baseVisibility * mods.VisibilityMod;
        }

        /// <summary>
        /// Gets effective resource yield.
        /// </summary>
        public static float GetEffectiveYield(
            float baseYield,
            in AccumulatedSpatialModifiers mods)
        {
            return baseYield * mods.YieldMod;
        }
    }
}

