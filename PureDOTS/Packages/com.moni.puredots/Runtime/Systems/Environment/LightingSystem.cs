using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Environment;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Computes lighting state from time of day (sun angle, intensity, color).
    /// </summary>
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    public partial struct LightingSystem : ISystem
    {
        private const float DAY_LENGTH_SECONDS = 300f; // 5 minutes per day
        private EntityArchetype _lightingArchetype;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _lightingArchetype = state.EntityManager.CreateArchetype(typeof(LightingState));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return;
            }

            // Compute sun angle from world seconds
            float dayProgress = (tickTime.WorldSeconds % DAY_LENGTH_SECONDS) / DAY_LENGTH_SECONDS;
            float sunAngle = dayProgress * math.PI * 2f; // 0 = sunrise, π = noon, 2π = sunset

            // Compute sun intensity (peak at noon, zero at night)
            float sunIntensity = math.max(0f, math.sin(sunAngle));
            
            // Compute sun color (warmer at sunrise/sunset, white at noon)
            float3 sunColor = new float3(1f, 0.95f, 0.9f); // Default warm white
            if (sunAngle < math.PI * 0.5f || sunAngle > math.PI * 1.5f)
            {
                // Sunrise/sunset: more orange/red
                sunColor = new float3(1f, 0.7f, 0.5f);
            }

            // Ambient intensity (higher during day)
            float ambientIntensity = 0.2f + sunIntensity * 0.3f;

            // Update or create LightingState singleton
            if (SystemAPI.TryGetSingletonEntity<LightingState>(out var lightingEntity))
            {
                SystemAPI.SetComponent(lightingEntity, new LightingState
                {
                    SunAngle = sunAngle,
                    SunIntensity = sunIntensity,
                    SunColor = sunColor,
                    AmbientIntensity = ambientIntensity
                });
            }
            else
            {
                var entity = state.EntityManager.CreateEntity(_lightingArchetype);
                state.EntityManager.SetComponentData(entity, new LightingState
                {
                    SunAngle = sunAngle,
                    SunIntensity = sunIntensity,
                    SunColor = sunColor,
                    AmbientIntensity = ambientIntensity
                });
            }
        }
    }
}

