using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Updates climate state with sine-wave/triangle oscillation for temperature and humidity.
    /// Handles seasonal progression if enabled.
    /// Runs in EnvironmentSystemGroup to provide climate data for other systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    public partial struct ClimateOscillationSystem : ISystem
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

            // Update climate state
            if (SystemAPI.TryGetSingletonRW<ClimateState>(out var climateState))
            {
                var climate = climateState.ValueRO;

                // Default oscillation parameters
                const float baseTemperature = 20f;
                const float temperatureOscillation = 10f;
                const float temperaturePeriod = 3600f; // ticks
                const float baseHumidity = 50f;
                const float humidityOscillation = 20f;
                const float humidityPeriod = 1800f; // ticks
                const uint seasonLengthTicks = 250u;

                // Calculate temperature oscillation
                var tempPhase = (float)(currentTick % (uint)temperaturePeriod) / temperaturePeriod;
                var tempOscillation = math.sin(tempPhase * 2f * math.PI) * temperatureOscillation;
                climate.GlobalTemperature = baseTemperature + tempOscillation;

                // Calculate humidity oscillation
                var humidityPhase = (float)(currentTick % (uint)humidityPeriod) / humidityPeriod;
                var humidityOscillationValue = math.sin(humidityPhase * 2f * math.PI) * humidityOscillation;
                climate.AtmosphericMoisture = math.clamp(baseHumidity + humidityOscillationValue, 0f, 100f);

                // Update seasons
                var seasonTick = currentTick % (seasonLengthTicks * 4u); // 4 seasons
                var seasonIndex = (byte)(seasonTick / seasonLengthTicks);
                climate.CurrentSeason = (Season)seasonIndex;
                climate.SeasonProgress = (float)(seasonTick % seasonLengthTicks) / seasonLengthTicks;

                climate.LastUpdateTick = currentTick;
                climateState.ValueRW = climate;
            }
        }
    }
}



