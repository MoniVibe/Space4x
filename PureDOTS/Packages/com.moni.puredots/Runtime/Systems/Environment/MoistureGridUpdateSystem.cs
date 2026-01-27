using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Environment;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Updates moisture grid cells based on sources (rain, miracles) and sinks (evaporation, consumption, drainage).
    /// Reuses SpatialGridConfig for cell layout.
    /// Runs in EnvironmentSystemGroup to provide moisture data for vegetation and other systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    public partial struct MoistureGridUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SpatialGridConfig>();
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

            // Get moisture config (use default if singleton doesn't exist)
            var moistureConfig = MoistureConfig.Default;
            if (SystemAPI.TryGetSingleton<MoistureConfig>(out var moistureConfigSingleton))
            {
                moistureConfig = moistureConfigSingleton;
            }

            // Get climate state for evaporation calculation
            var climateState = ClimateStateDefaults.Default;
            if (SystemAPI.TryGetSingleton<ClimateState>(out var climateSingleton))
            {
                climateState = climateSingleton;
            }

            // Get wind state for evaporation calculation
            var windState = WindStateDefaults.Default;
            if (SystemAPI.TryGetSingleton<WindState>(out var windSingleton))
            {
                windState = windSingleton;
            }

            // Update moisture grid state
            if (SystemAPI.TryGetSingletonRW<MoistureGridState>(out var moistureGridState))
            {
                var gridState = moistureGridState.ValueRO;

                // Skip if grid not initialized or update frequency not met
                if (!gridState.Grid.IsCreated || 
                    (currentTick - gridState.LastUpdateTick) < moistureConfig.UpdateFrequency)
                {
                    return;
                }

                // TODO: Update moisture cells
                // For Tier-1, we'll update the grid blob in a future iteration
                // This requires rebuilding the blob asset, which is complex
                // For now, mark as updated

                var updatedGridState = gridState;
                updatedGridState.LastUpdateTick = currentTick;
                moistureGridState.ValueRW = updatedGridState;
            }
        }

        /// <summary>
        /// Calculates evaporation rate based on temperature and wind.
        /// Public for testing purposes.
        /// </summary>
        [BurstCompile]
        public static float CalculateEvaporationRate(
            float baseRate,
            float temperature,
            float windStrength,
            in MoistureConfig config)
        {
            var tempFactor = 1f + (temperature * config.TemperatureEvaporationMultiplier);
            var windFactor = 1f + (windStrength * config.WindEvaporationMultiplier);
            return baseRate * tempFactor * windFactor;
        }
    }

    /// <summary>
    /// Default ClimateState for when singleton doesn't exist.
    /// </summary>
    internal static class ClimateStateDefaults
    {
        public static ClimateState Default => new ClimateState
        {
            CurrentSeason = Season.Spring,
            SeasonProgress = 0f,
            TimeOfDayHours = 12f,
            DayNightProgress = 0.5f,
            GlobalTemperature = 20f,
            GlobalWindDirection = new float2(1f, 0f),
            GlobalWindStrength = 5f,
            AtmosphericMoisture = 50f,
            CloudCover = 30f,
            LastUpdateTick = 0
        };
    }

    /// <summary>
    /// Default WindState for when singleton doesn't exist.
    /// </summary>
    internal static class WindStateDefaults
    {
        public static PureDOTS.Runtime.Environment.WindState Default => new PureDOTS.Runtime.Environment.WindState
        {
            Direction = new float2(1f, 0f), // East
            Strength = 0.3f,
            Type = WindType.Breeze,
            LastUpdateTick = 0
        };
    }
}

