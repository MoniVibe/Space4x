using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rendering;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Smoothly adjusts vegetation placeholder scale based on lifecycle data.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VegetationSystemGroup))]
    [UpdateAfter(typeof(VegetationGrowthSystem))]
    public partial struct VegetationPlaceholderScaleSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlaceholderVegetationScale>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, scaleState, settings, lifecycle) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PlaceholderVegetationScaleState>, RefRO<PlaceholderVegetationScale>, RefRO<VegetationLifecycle>>())
            {
                float targetScale = DetermineTargetScale(settings.ValueRO, lifecycle.ValueRO);

                float lerpFactor = settings.ValueRO.LerpSeconds > 0f
                    ? math.saturate(deltaTime / math.max(settings.ValueRO.LerpSeconds, 1e-3f))
                    : 1f;

                ref var scaleStateRef = ref scaleState.ValueRW;
                float initialScale = scaleStateRef.CurrentScale <= 0f
                    ? settings.ValueRO.SeedlingScale
                    : scaleStateRef.CurrentScale;

                float newScale = math.lerp(initialScale, targetScale, lerpFactor);
                scaleStateRef.CurrentScale = newScale;
                ref var localTransform = ref transform.ValueRW;
                localTransform.Scale = newScale;
            }
        }

        private static float DetermineTargetScale(in PlaceholderVegetationScale settings, in VegetationLifecycle lifecycle)
        {
            return lifecycle.CurrentStage switch
            {
                VegetationLifecycle.LifecycleStage.Seedling =>
                    settings.SeedlingScale,
                VegetationLifecycle.LifecycleStage.Growing =>
                    math.lerp(settings.SeedlingScale, settings.GrowingScale, math.saturate(lifecycle.GrowthProgress)),
                VegetationLifecycle.LifecycleStage.Mature =>
                    math.lerp(settings.GrowingScale, settings.MatureScale, math.saturate(lifecycle.GrowthProgress)),
                VegetationLifecycle.LifecycleStage.Flowering =>
                    math.lerp(settings.MatureScale, settings.FruitingScale, math.saturate(lifecycle.GrowthProgress)),
                VegetationLifecycle.LifecycleStage.Fruiting =>
                    settings.FruitingScale,
                VegetationLifecycle.LifecycleStage.Dying =>
                    math.lerp(settings.FruitingScale, settings.DyingScale, math.saturate(lifecycle.GrowthProgress)),
                VegetationLifecycle.LifecycleStage.Dead =>
                    settings.DeadScale,
                _ => settings.MatureScale
            };
        }
    }

    /// <summary>
    /// Pulses miracle placeholder color/emission to help them stand out.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct MiraclePlaceholderPulseSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiraclePlaceholderPulse>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (pulse, baseColor, emissionColor) in
                     SystemAPI.Query<RefRW<MiraclePlaceholderPulse>, RefRW<URPMaterialPropertyBaseColor>, RefRW<URPMaterialPropertyEmissionColor>>())
            {
                ref var pulseRef = ref pulse.ValueRW;
                float phase = pulseRef.Phase + deltaTime * pulseRef.PulseSpeed;
                if (phase > math.PI * 2f)
                {
                    phase -= math.PI * 2f;
                }

                pulseRef.Phase = phase;

                float intensity = math.max(0f, pulseRef.BaseIntensity + pulseRef.PulseAmplitude * math.sin(phase));
                float3 rgb = pulseRef.BaseColor.xyz * intensity;
                ref var baseColorRef = ref baseColor.ValueRW;
                baseColorRef.Value = new float4(rgb, pulseRef.BaseColor.w);
                ref var emissionRef = ref emissionColor.ValueRW;
                emissionRef.Value = new float4(rgb, 1f);
            }
        }
    }

    /// <summary>
    /// Applies simple material overrides to URP material property components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(MiraclePlaceholderPulseSystem))]
    public partial struct MaterialOverrideSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) && rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            foreach (var (overrideColor, baseColor) in SystemAPI
                         .Query<RefRO<MaterialColorOverride>, RefRW<URPMaterialPropertyBaseColor>>())
            {
                baseColor.ValueRW.Value = overrideColor.ValueRO.Value;
            }

            foreach (var (overrideEmission, emission) in SystemAPI
                         .Query<RefRO<MaterialEmissionOverride>, RefRW<URPMaterialPropertyEmissionColor>>())
            {
                emission.ValueRW.Value = overrideEmission.ValueRO.Value;
            }
        }
    }
}
