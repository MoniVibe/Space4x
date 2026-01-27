using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Environment;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Updates wind state direction and strength based on configuration.
    /// Optional oscillation or random variation.
    /// Runs in EnvironmentSystemGroup to provide wind data for other systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    public partial struct WindUpdateSystem : ISystem
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
            var config = WindConfig.Default;
            if (SystemAPI.TryGetSingleton<WindConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            // Update wind state
            if (SystemAPI.TryGetSingletonRW<WindState>(out var windState))
            {
                var wind = windState.ValueRO;
                var configRef = config;

                // Update wind direction (slow rotation)
                var directionAngle = math.atan2(wind.Direction.y, wind.Direction.x);
                directionAngle += configRef.DirectionChangeRate;
                wind.Direction = new float2(math.cos(directionAngle), math.sin(directionAngle));

                // Update wind strength with oscillation
                var strengthPhase = (float)(currentTick % configRef.StrengthPeriod) / configRef.StrengthPeriod;
                var strengthOscillation = math.sin(strengthPhase * 2f * math.PI) * configRef.StrengthOscillation;
                wind.Strength = math.clamp(configRef.BaseStrength + strengthOscillation, 0f, 1f);

                // Determine wind type based on strength thresholds
                if (wind.Strength < configRef.BreezeThreshold)
                {
                    wind.Type = WindType.Calm;
                }
                else if (wind.Strength < configRef.WindThreshold)
                {
                    wind.Type = WindType.Breeze;
                }
                else if (wind.Strength < configRef.StormThreshold)
                {
                    wind.Type = WindType.Wind;
                }
                else
                {
                    wind.Type = WindType.Storm;
                }

                wind.LastUpdateTick = currentTick;
                windState.ValueRW = wind;
            }
        }
    }
}
























