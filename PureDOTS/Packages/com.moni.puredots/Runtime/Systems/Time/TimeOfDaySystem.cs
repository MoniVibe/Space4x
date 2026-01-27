using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Time
{
    /// <summary>
    /// Computes time-of-day and sunlight factor from orbital phase.
    /// Burst-compiled system that maps OrbitState → TimeOfDayState and SunlightFactor.
    /// Runs in EnvironmentSystemGroup after OrbitAdvanceSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(OrbitAdvanceSystem))]
    public partial struct TimeOfDaySystem : ISystem
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
            if (timeState.IsPaused)
            {
                return;
            }

            // Skip if paused or rewinding
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Get default config (fallback if planet doesn't have config)
            var defaultConfig = TimeOfDayConfig.Default;

            // Process all planets with orbit state
            foreach (var (orbitParams, orbitState, timeOfDayState, sunlightFactor, config, entity) in SystemAPI.Query<
                RefRO<OrbitParameters>,
                RefRO<OrbitState>,
                RefRW<TimeOfDayState>,
                RefRW<SunlightFactor>,
                RefRO<TimeOfDayConfig>>().WithEntityAccess())
            {
                // Get orbital phase
                var orbitalPhase = orbitState.ValueRO.OrbitalPhase;

                // Apply time-of-day offset if specified
                var timeOfDayNorm = math.frac(orbitalPhase + orbitParams.ValueRO.TimeOfDayOffset);

                // Use per-planet config
                var planetConfig = config.ValueRO;

                // Determine phase based on thresholds
                var phase = DeterminePhase(timeOfDayNorm, in planetConfig);

                // Calculate sunlight factor using cosine curve
                // Cosine gives smooth transition: 0 at midnight, 1 at noon, 0 at midnight
                var sunlight = CalculateSunlight(timeOfDayNorm, in planetConfig);

                // Apply solar yield from parent star if planet has StarParent
                if (SystemAPI.HasComponent<StarParent>(entity))
                {
                    var starParent = SystemAPI.GetComponent<StarParent>(entity);
                    var parentStar = starParent.ParentStar;

                    if (parentStar != Entity.Null && SystemAPI.Exists(parentStar))
                    {
                        if (SystemAPI.HasComponent<StarSolarYield>(parentStar))
                        {
                            var starYield = SystemAPI.GetComponent<StarSolarYield>(parentStar);
                            // Multiply sunlight by star's solar yield
                            sunlight *= starYield.Yield;
                        }
                    }
                }

                // Update time-of-day state
                var previousPhase = timeOfDayState.ValueRO.Phase;
                timeOfDayState.ValueRW.TimeOfDayNorm = timeOfDayNorm;
                timeOfDayState.ValueRW.PreviousPhase = previousPhase;
                timeOfDayState.ValueRW.Phase = phase;

                // Update sunlight factor
                sunlightFactor.ValueRW.Sunlight = sunlight;
            }
        }

        /// <summary>
        /// Determines the time-of-day phase based on normalized time and config thresholds.
        /// </summary>
        [BurstCompile]
        private static TimeOfDayPhase DeterminePhase(float timeOfDayNorm, in TimeOfDayConfig config)
        {
            // Normalize thresholds to ensure they're in [0, 1)
            var dawn = math.frac(config.DawnThreshold);
            var day = math.frac(config.DayThreshold);
            var dusk = math.frac(config.DuskThreshold);
            var night = math.frac(config.NightThreshold);

            // Determine phase based on thresholds
            // Handle wrap-around case where night threshold > dawn threshold
            if (night > dawn)
            {
                // Standard case: Dawn < Day < Dusk < Night
                if (timeOfDayNorm >= dawn && timeOfDayNorm < day)
                    return TimeOfDayPhase.Dawn;
                if (timeOfDayNorm >= day && timeOfDayNorm < dusk)
                    return TimeOfDayPhase.Day;
                if (timeOfDayNorm >= dusk && timeOfDayNorm < night)
                    return TimeOfDayPhase.Dusk;
                // Night: [night, 1.0) or [0.0, dawn)
                return TimeOfDayPhase.Night;
            }
            else
            {
                // Wrap-around case: Night spans across midnight
                if (timeOfDayNorm >= night || timeOfDayNorm < dawn)
                    return TimeOfDayPhase.Night;
                if (timeOfDayNorm >= dawn && timeOfDayNorm < day)
                    return TimeOfDayPhase.Dawn;
                if (timeOfDayNorm >= day && timeOfDayNorm < dusk)
                    return TimeOfDayPhase.Day;
                return TimeOfDayPhase.Dusk;
            }
        }

        /// <summary>
        /// Calculates sunlight factor [0..1] based on normalized time-of-day.
        /// Uses a cosine curve to provide smooth transitions.
        /// </summary>
        [BurstCompile]
        private static float CalculateSunlight(float timeOfDayNorm, in TimeOfDayConfig config)
        {
            // Cosine curve: cos((time - 0.5) * 2π) gives:
            // - 1.0 at time = 0.5 (noon)
            // - -1.0 at time = 0.0 and 1.0 (midnight)
            // Map to [0, 1] range: (cos + 1) / 2
            var cosine = math.cos((timeOfDayNorm - 0.5f) * 2f * math.PI);
            var normalized = (cosine + 1f) * 0.5f;

            // Apply min/max sunlight range
            var sunlight = math.lerp(config.MinSunlight, config.MaxSunlight, normalized);

            // Clamp to [0, 1]
            return math.clamp(sunlight, 0f, 1f);
        }
    }
}

