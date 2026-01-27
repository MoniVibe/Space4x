using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Environment;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems.Environment;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Time
{
    /// <summary>
    /// Integrates sunlight factor from orbit/time-of-day system into vegetation environment state.
    /// Updates VegetationEnvironmentState.Light field based on SunlightFactor from the planet.
    /// Runs in VegetationSystemGroup before VegetationHealthSystem so health calculations use updated light.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VegetationSystemGroup))]
    [UpdateBefore(typeof(VegetationHealthSystem))]
    public partial struct VegetationSunlightIntegrationSystem : ISystem
    {
        private EnvironmentSampler _environmentSampler;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _environmentSampler = new EnvironmentSampler(ref state);
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

            _environmentSampler.Update(ref state);

            var globalSunlight = ResolveGlobalSunlight(ref state);
            var fallbackSample = new SunlightSample
            {
                DirectLight = globalSunlight * 100f,
                AmbientLight = globalSunlight * 20f,
                OccluderCount = 0
            };

            foreach (var (envState, transform) in SystemAPI.Query<RefRW<VegetationEnvironmentState>, RefRO<LocalTransform>>())
            {
                var sunlight = _environmentSampler.SampleSunlightDetailed(transform.ValueRO.Position, fallbackSample).Value;
                var lightScalar = math.saturate((sunlight.DirectLight + sunlight.AmbientLight) / 100f);

                envState.ValueRW.Light = lightScalar;
                envState.ValueRW.LastSampleTick = timeState.Tick;
            }
        }

        private float ResolveGlobalSunlight(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<SunlightState>(out var sunlightState))
            {
                return math.saturate(sunlightState.GlobalIntensity);
            }

            if (SystemAPI.TryGetSingleton<SunlightFactor>(out var sunlightFactor))
            {
                return math.saturate(sunlightFactor.Sunlight);
            }

            foreach (var sunlight in SystemAPI.Query<RefRO<SunlightFactor>>())
            {
                return math.saturate(sunlight.ValueRO.Sunlight);
            }

            return 1f;
        }
    }
}
