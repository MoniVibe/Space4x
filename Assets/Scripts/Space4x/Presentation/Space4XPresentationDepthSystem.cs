using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    public partial struct Space4XPresentationDepthSystem : ISystem
    {
        private ComponentLookup<PresentationScaleMultiplier> _scaleMultiplierLookup;
        private const float CarrierAmplitude = 2.5f;
        private const float VesselAmplitude = 1.25f;
        private const float StrikeCraftAmplitude = 1.1f;
        private const float PickupAmplitude = 0.9f;
        private const float AsteroidAmplitude = 3.5f;

        private const float CarrierFrequency = 0.08f;
        private const float VesselFrequency = 0.16f;
        private const float StrikeCraftFrequency = 0.22f;
        private const float PickupFrequency = 0.28f;
        private const float AsteroidFrequency = 0.05f;

        private const float DefaultCarrierScale = 0.5f;
        private const float DefaultCraftScale = 0.02f;
        private const float DefaultStrikeCraftScale = 0.012f;
        private const float DefaultPickupScale = 0.015f;
        private const float DefaultAsteroidScale = 20f;
        private const float DefaultProjectileScale = 0.008f;
        private const float DefaultFleetImpostorScale = 0.4f;
        private const float AsteroidMinScaleMultiplier = 0.6f;
        private const float AsteroidMaxScaleMultiplier = 2.5f;
        private const float AsteroidReferenceAmount = 500f;
        private const float AsteroidResourceScaleMin = 0.25f;
        private const float AsteroidResourceScaleMax = 6f;
        private const float AsteroidJitterMin = 0.85f;
        private const float AsteroidJitterMax = 1.15f;
        private const float CarrierBaseOffset = 5f;
        private const float CraftBaseOffset = 8f;
        private const float StrikeCraftBaseOffset = 9f;
        private const float PickupBaseOffset = 6f;
        private const float AsteroidBaseOffset = 14f;
        private const float DefaultIndividualScale = 0.003f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _scaleMultiplierLookup = state.GetComponentLookup<PresentationScaleMultiplier>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            var time = (float)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ResolveScaleDefaults(ref state, out var carrierScale, out var craftScale, out var asteroidBaseScale);
            _scaleMultiplierLookup.Update(ref state);

            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<CarrierPresentationTag>, RefRO<LocalTransform>>()
                         .WithNone<LocalToWorld>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new LocalToWorld { Value = float4x4.identity });
            }

            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<CraftPresentationTag>, RefRO<LocalTransform>>()
                         .WithNone<LocalToWorld>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new LocalToWorld { Value = float4x4.identity });
            }

            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<StrikeCraftPresentationTag>, RefRO<LocalTransform>>()
                         .WithNone<LocalToWorld>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new LocalToWorld { Value = float4x4.identity });
            }

            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<ResourcePickupPresentationTag>, RefRO<LocalTransform>>()
                         .WithNone<LocalToWorld>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new LocalToWorld { Value = float4x4.identity });
            }

            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<AsteroidPresentationTag>, RefRO<LocalTransform>>()
                         .WithNone<LocalToWorld>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new LocalToWorld { Value = float4x4.identity });
            }

            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<ProjectilePresentationTag>, RefRO<LocalTransform>>()
                         .WithNone<LocalToWorld>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new LocalToWorld { Value = float4x4.identity });
            }

            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<FleetImpostorTag>, RefRO<LocalTransform>>()
                         .WithNone<LocalToWorld>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new LocalToWorld { Value = float4x4.identity });
            }

            foreach (var (_, _, entity) in SystemAPI
                         .Query<RefRO<IndividualPresentationTag>, RefRO<LocalTransform>>()
                         .WithNone<LocalToWorld>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new LocalToWorld { Value = float4x4.identity });
            }

            ecb.Playback(state.EntityManager);
            _scaleMultiplierLookup.Update(ref state);
            ecb.Dispose();

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<CarrierPresentationTag>()
                         .WithEntityAccess())
            {
                float phase = PhaseFromEntity(entity);
                float baseOffset = HashToSignedUnit(entity, 31) * CarrierBaseOffset;
                float offset = baseOffset + math.sin(time * CarrierFrequency + phase) * CarrierAmplitude;
                float baseScale = carrierScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.01f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = transform.ValueRO.Scale * baseScale * scaleMultiplier;
                float3 position = transform.ValueRO.Position + new float3(0f, offset, 0f);
                localToWorld.ValueRW.Value = float4x4.TRS(position, transform.ValueRO.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                float phase = PhaseFromEntity(entity);
                float baseOffset = HashToSignedUnit(entity, 47) * CraftBaseOffset;
                float offset = baseOffset + math.sin(time * VesselFrequency + phase) * VesselAmplitude;
                float baseScale = craftScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.005f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = transform.ValueRO.Scale * baseScale * scaleMultiplier;
                float3 position = transform.ValueRO.Position + new float3(0f, offset, 0f);
                localToWorld.ValueRW.Value = float4x4.TRS(position, transform.ValueRO.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<StrikeCraftPresentationTag>()
                         .WithEntityAccess())
            {
                float phase = PhaseFromEntity(entity);
                float baseOffset = HashToSignedUnit(entity, 53) * StrikeCraftBaseOffset;
                float offset = baseOffset + math.sin(time * StrikeCraftFrequency + phase) * StrikeCraftAmplitude;
                float baseScale = DefaultStrikeCraftScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.003f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = transform.ValueRO.Scale * baseScale * scaleMultiplier;
                float3 position = transform.ValueRO.Position + new float3(0f, offset, 0f);
                localToWorld.ValueRW.Value = float4x4.TRS(position, transform.ValueRO.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<ResourcePickupPresentationTag>()
                         .WithEntityAccess())
            {
                float phase = PhaseFromEntity(entity);
                float baseOffset = HashToSignedUnit(entity, 61) * PickupBaseOffset;
                float offset = baseOffset + math.sin(time * PickupFrequency + phase) * PickupAmplitude;
                float baseScale = DefaultPickupScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.004f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = transform.ValueRO.Scale * baseScale * scaleMultiplier;
                float3 position = transform.ValueRO.Position + new float3(0f, offset, 0f);
                localToWorld.ValueRW.Value = float4x4.TRS(position, transform.ValueRO.Rotation, new float3(scale));
            }

            foreach (var (asteroid, transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<Asteroid>, RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<AsteroidPresentationTag>()
                         .WithEntityAccess())
            {
                float phase = PhaseFromEntity(entity);
                float baseOffset = HashToSignedUnit(entity, 79) * AsteroidBaseOffset;
                float offset = baseOffset + math.sin(time * AsteroidFrequency + phase) * AsteroidAmplitude;
                float ratio = asteroid.ValueRO.MaxResourceAmount > 0f
                    ? asteroid.ValueRO.ResourceAmount / asteroid.ValueRO.MaxResourceAmount
                    : 1f;
                float baseScale = asteroidBaseScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.1f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                else
                {
                    baseScale = ResolveAsteroidResourceScale(baseScale, asteroid.ValueRO.MaxResourceAmount);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float minScale = baseScale * AsteroidMinScaleMultiplier;
                float maxScale = baseScale * AsteroidMaxScaleMultiplier;
                float scaleFromRatio = math.lerp(minScale, maxScale, math.saturate(ratio));
                float jitter = math.lerp(AsteroidJitterMin, AsteroidJitterMax, HashToUnit(entity, 97));
                float scale = transform.ValueRO.Scale * scaleFromRatio * jitter * scaleMultiplier;
                float3 position = transform.ValueRO.Position + new float3(0f, offset, 0f);
                localToWorld.ValueRW.Value = float4x4.TRS(position, transform.ValueRO.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<ProjectilePresentationTag>()
                         .WithEntityAccess())
            {
                float baseScale = DefaultProjectileScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.001f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = transform.ValueRO.Scale * baseScale * scaleMultiplier;
                localToWorld.ValueRW.Value = float4x4.TRS(transform.ValueRO.Position, transform.ValueRO.Rotation, new float3(scale));
            }

            foreach (var (iconMesh, transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<FleetIconMesh>, RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<FleetImpostorTag>()
                         .WithEntityAccess())
            {
                float baseScale = DefaultFleetImpostorScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.1f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                else
                {
                    baseScale = math.max(baseScale, iconMesh.ValueRO.Size);
                }

                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = transform.ValueRO.Scale * baseScale * scaleMultiplier;
                localToWorld.ValueRW.Value = float4x4.TRS(transform.ValueRO.Position, transform.ValueRO.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<IndividualPresentationTag>()
                         .WithEntityAccess())
            {
                float baseScale = DefaultIndividualScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.001f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }

                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = transform.ValueRO.Scale * baseScale * scaleMultiplier;
                localToWorld.ValueRW.Value = float4x4.TRS(transform.ValueRO.Position, transform.ValueRO.Rotation, new float3(scale));
            }
        }

        private void ResolveScaleDefaults(
            ref SystemState state,
            out float carrierScale,
            out float craftScale,
            out float asteroidBaseScale)
        {
            carrierScale = DefaultCarrierScale;
            craftScale = DefaultCraftScale;
            asteroidBaseScale = DefaultAsteroidScale;

            if (SystemAPI.TryGetSingleton<Space4XMiningVisualConfig>(out var visualConfig))
            {
                carrierScale = math.max(0.01f, visualConfig.CarrierScale);
                craftScale = math.max(0.005f, visualConfig.MiningVesselScale);
                asteroidBaseScale = math.max(0.5f, visualConfig.AsteroidScale);
            }
        }

        private static float ResolveAsteroidResourceScale(float baseScale, float maxResourceAmount)
        {
            var normalized = math.max(0.01f, maxResourceAmount / AsteroidReferenceAmount);
            var factor = math.sqrt(normalized);
            factor = math.clamp(factor, AsteroidResourceScaleMin, AsteroidResourceScaleMax);
            return baseScale * factor;
        }

        private static float PhaseFromEntity(Entity entity)
        {
            uint hash = (uint)math.hash(new int2(entity.Index, entity.Version));
            return (hash / (float)uint.MaxValue) * math.PI * 2f;
        }

        private static float HashToUnit(Entity entity, int salt)
        {
            uint hash = (uint)math.hash(new int3(entity.Index, entity.Version, salt));
            return hash / (float)uint.MaxValue;
        }

        private static float HashToSignedUnit(Entity entity, int salt)
        {
            return HashToUnit(entity, salt) * 2f - 1f;
        }

        private float ResolveScaleMultiplier(Entity entity)
        {
            if (_scaleMultiplierLookup.HasComponent(entity))
            {
                return math.max(0.001f, _scaleMultiplierLookup[entity].Value);
            }

            return 1f;
        }
    }
}
