using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Environment;
using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems.Space;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Distributes sunlight from StarSolarYield to worlds/planets.
    /// Tier-1: Global sunlight per planet/world.
    /// Tier-2+: Per-cell sunlight factoring in time-of-day, terrain shadowing.
    /// Integrates with existing TimeOfDaySystem and StarSolarYieldSystem.
    /// Runs in EnvironmentSystemGroup after StarSolarYieldSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Space.StarSolarYieldSystem))]
    public partial struct SunlightDistributionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;

            // Get sunlight config (use default if singleton doesn't exist)
            var sunlightConfig = SunlightConfig.Default;
            if (SystemAPI.TryGetSingleton<SunlightConfig>(out var sunlightConfigSingleton))
            {
                sunlightConfig = sunlightConfigSingleton;
            }

            // Process planets/worlds with StarParent
            foreach (var (starParent, sunlightState, entity) in SystemAPI.Query<
                RefRO<StarParent>,
                RefRW<SunlightState>>().WithEntityAccess())
            {
                var parentStar = starParent.ValueRO.ParentStar;

                // Skip if no parent star (orbits galactic center directly)
                if (parentStar == Entity.Null)
                {
                    continue;
                }

                // Get star's solar yield
                if (!SystemAPI.HasComponent<StarSolarYield>(parentStar))
                {
                    continue;
                }

                var starYield = SystemAPI.GetComponent<StarSolarYield>(parentStar);
                var sunlight = sunlightState.ValueRO;

                // Calculate global sunlight intensity from star yield
                var baseIntensity = starYield.Yield * sunlightConfig.BaseMultiplier;

                // Apply time-of-day factor if TimeOfDayState exists
                if (SystemAPI.HasComponent<TimeOfDayState>(entity))
                {
                    var timeOfDayState = SystemAPI.GetComponent<TimeOfDayState>(entity);
                    var timeOfDayNorm = timeOfDayState.TimeOfDayNorm; // 0 = midnight, 0.5 = noon, 1 = midnight
                    
                    // Convert to sunlight curve (0 at midnight, 1 at noon)
                    var sunlightCurve = 1f - math.abs(timeOfDayNorm - 0.5f) * 2f; // Triangle wave
                    sunlightCurve = math.max(0f, sunlightCurve); // Clamp to 0-1
                    
                    baseIntensity *= sunlightCurve * sunlightConfig.TimeOfDayFactor + (1f - sunlightConfig.TimeOfDayFactor);
                }

                // Clamp to configured range
                sunlight.GlobalIntensity = math.clamp(baseIntensity, sunlightConfig.MinSunlight, sunlightConfig.MaxSunlight);
                sunlight.SourceStar = parentStar;
                sunlight.LastUpdateTick = currentTick;
                sunlightState.ValueRW = sunlight;
            }

            // Also handle worlds without StarParent (Godgame single-world case)
            // Use a default star or global sunlight value
            foreach (var (sunlightState, entity) in SystemAPI.Query<RefRW<SunlightState>>()
                .WithNone<StarParent>().WithEntityAccess())
            {
                var sunlight = sunlightState.ValueRO;

                // If no source star set, try to find a default star or use config default
                if (sunlight.SourceStar == Entity.Null)
                {
                    // Try to find any star with high yield as default
                    float bestYield = 0f;
                    Entity bestStar = Entity.Null;

                    foreach (var (yield, starEntity) in SystemAPI.Query<RefRO<StarSolarYield>>()
                        .WithEntityAccess())
                    {
                        if (yield.ValueRO.Yield > bestYield)
                        {
                            bestYield = yield.ValueRO.Yield;
                            bestStar = starEntity;
                        }
                    }

                    if (bestStar != Entity.Null)
                    {
                        sunlight.SourceStar = bestStar;
                        sunlight.GlobalIntensity = bestYield * sunlightConfig.BaseMultiplier;
                    }
                    else
                    {
                        // No stars found, use default sunlight
                        sunlight.GlobalIntensity = sunlightConfig.BaseMultiplier;
                    }
                }

                sunlight.LastUpdateTick = currentTick;
                sunlightState.ValueRW = sunlight;
            }
        }
    }
}

