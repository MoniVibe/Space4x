using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Space
{
    /// <summary>
    /// Calculates solar yield from star luminosity based on configurable strategy.
    /// Burst-compiled system that updates StarSolarYield component.
    /// Runs in EnvironmentSystemGroup to provide yield data for other systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    public partial struct StarSolarYieldSystem : ISystem
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

            var currentTick = timeState.Tick;

            // Get config (use default if singleton doesn't exist)
            var config = StarSolarYieldConfig.Default;
            if (SystemAPI.TryGetSingleton<StarSolarYieldConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            // Process all stars with luminosity
            foreach (var (luminosity, yield) in SystemAPI.Query<
                RefRO<StarLuminosity>,
                RefRW<StarSolarYield>>())
            {
                // Calculate yield based on strategy
                var calculatedYield = CalculateYield(luminosity.ValueRO.Luminosity, in config);

                // Update yield component
                yield.ValueRW.Yield = calculatedYield;
                yield.ValueRW.LastCalculationTick = currentTick;
            }
        }

        /// <summary>
        /// Calculates solar yield from luminosity using the configured strategy.
        /// Public for testing purposes.
        /// </summary>
        [BurstCompile]
        public static float CalculateYield(float luminosity, in StarSolarYieldConfig config)
        {
            // Clamp luminosity to valid range
            var clampedLuminosity = math.max(0f, luminosity);

            return config.Strategy switch
            {
                SolarYieldStrategy.Normalize => CalculateNormalizedYield(clampedLuminosity, in config),
                SolarYieldStrategy.Logarithmic => CalculateLogarithmicYield(clampedLuminosity, in config),
                SolarYieldStrategy.Custom => CalculateCustomYield(clampedLuminosity, in config),
                _ => 0f
            };
        }

        /// <summary>
        /// Calculates yield using normalization: (luminosity - min) / (max - min).
        /// Public for testing purposes.
        /// </summary>
        [BurstCompile]
        public static float CalculateNormalizedYield(float luminosity, in StarSolarYieldConfig config)
        {
            var range = config.MaxLuminosity - config.MinLuminosity;
            if (range <= 0f)
                return 0f;

            var normalized = (luminosity - config.MinLuminosity) / range;
            return math.clamp(normalized, 0f, 1f);
        }

        /// <summary>
        /// Calculates yield using logarithmic scale: log(luminosity) / log(maxLuminosity).
        /// Public for testing purposes.
        /// </summary>
        [BurstCompile]
        public static float CalculateLogarithmicYield(float luminosity, in StarSolarYieldConfig config)
        {
            if (luminosity <= 0f || config.MaxLuminosity <= 0f)
                return 0f;

            // Use natural logarithm and scale
            var logLuminosity = math.log(luminosity);
            var logMax = math.log(config.MaxLuminosity);

            if (logMax <= 0f)
                return 0f;

            var normalized = logLuminosity / logMax;
            return math.clamp(normalized, 0f, 1f);
        }

        /// <summary>
        /// Calculates yield using custom formula: multiplier * (luminosity ^ exponent).
        /// Public for testing purposes.
        /// </summary>
        [BurstCompile]
        public static float CalculateCustomYield(float luminosity, in StarSolarYieldConfig config)
        {
            if (luminosity <= 0f)
                return 0f;

            var powered = math.pow(luminosity, config.CustomExponent);
            var result = config.CustomMultiplier * powered;
            return math.clamp(result, 0f, 1f);
        }
    }
}

