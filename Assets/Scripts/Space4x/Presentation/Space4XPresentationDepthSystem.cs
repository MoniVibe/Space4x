using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    public partial struct Space4XPresentationDepthSystem : ISystem
    {
        private ComponentLookup<PresentationScaleMultiplier> _scaleMultiplierLookup;
        private ComponentLookup<SimPoseSnapshot> _poseSnapshotLookup;
        private ComponentLookup<Space4XOrbitalBandState> _orbitalBandStateLookup;
        private ComponentLookup<Space4XFrameTransform> _frameTransformLookup;
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
        private const float AsteroidJitterMin = 0.85f;
        private const float AsteroidJitterMax = 1.15f;
        private const float CarrierBaseOffset = 5f;
        private const float CraftBaseOffset = 8f;
        private const float StrikeCraftBaseOffset = 9f;
        private const float PickupBaseOffset = 6f;
        private const float AsteroidBaseOffset = 14f;
        private const float DefaultIndividualScale = 0.003f;

        public void OnCreate(ref SystemState state)
        {
            _scaleMultiplierLookup = state.GetComponentLookup<PresentationScaleMultiplier>(true);
            _poseSnapshotLookup = state.GetComponentLookup<SimPoseSnapshot>(true);
            _orbitalBandStateLookup = state.GetComponentLookup<Space4XOrbitalBandState>(true);
            _frameTransformLookup = state.GetComponentLookup<Space4XFrameTransform>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var disableDepthOffset = false;
            if (SystemAPI.TryGetSingleton<Space4XPresentationDebugConfig>(out var debugConfig))
            {
                disableDepthOffset = debugConfig.DisableDepthBobbing != 0;
            }
            if (SystemAPI.TryGetSingleton<Space4XMiningVisualConfig>(out var visualConfig))
            {
                disableDepthOffset |= visualConfig.DisableDepthOffset != 0;
            }

            var time = (float)SystemAPI.Time.ElapsedTime;
            var alpha = 1f;
            if (SystemAPI.TryGetSingleton<FixedStepInterpolationState>(out var interpolation))
            {
                alpha = math.saturate(interpolation.Alpha);
            }
            var useBandScale = false;
            var useRenderFrame = false;
            var renderFrame = default(Space4XRenderFrameState);
            if (SystemAPI.TryGetSingleton<Space4XOrbitalBandConfig>(out var bandConfig))
            {
                useBandScale = bandConfig.Enabled != 0;
            }
            if (SystemAPI.TryGetSingleton<Space4XRenderFrameState>(out var renderFrameState) &&
                SystemAPI.TryGetSingleton<Space4XRenderFrameConfig>(out var renderFrameConfig) &&
                renderFrameConfig.Enabled != 0)
            {
                useRenderFrame = true;
                renderFrame = renderFrameState;
                useBandScale = false;
            }
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ResolveScaleDefaults(ref state, out var carrierScale, out var craftScale, out var asteroidBaseScale);
            _scaleMultiplierLookup.Update(ref state);
            _poseSnapshotLookup.Update(ref state);
            if (useBandScale)
            {
                _orbitalBandStateLookup.Update(ref state);
                _frameTransformLookup.Update(ref state);
            }

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
            _poseSnapshotLookup.Update(ref state);
            ecb.Dispose();

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<CarrierPresentationTag>()
                         .WithEntityAccess())
            {
                var pose = ResolvePose(entity, transform.ValueRO, alpha, useBandScale, useRenderFrame, renderFrame);
                float phase = PhaseFromEntity(entity);
                float baseOffset = HashToSignedUnit(entity, 31) * CarrierBaseOffset;
                float offset = disableDepthOffset ? 0f : baseOffset + math.sin(time * CarrierFrequency + phase) * CarrierAmplitude;
                float baseScale = carrierScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.01f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = pose.Scale * baseScale * scaleMultiplier;
                float3 position = pose.Position + new float3(0f, offset, 0f);
                localToWorld.ValueRW.Value = float4x4.TRS(position, pose.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<CraftPresentationTag>()
                         .WithEntityAccess())
            {
                var pose = ResolvePose(entity, transform.ValueRO, alpha, useBandScale, useRenderFrame, renderFrame);
                float phase = PhaseFromEntity(entity);
                float baseOffset = HashToSignedUnit(entity, 47) * CraftBaseOffset;
                float offset = disableDepthOffset ? 0f : baseOffset + math.sin(time * VesselFrequency + phase) * VesselAmplitude;
                float baseScale = craftScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.005f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = pose.Scale * baseScale * scaleMultiplier;
                float3 position = pose.Position + new float3(0f, offset, 0f);
                localToWorld.ValueRW.Value = float4x4.TRS(position, pose.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<StrikeCraftPresentationTag>()
                         .WithEntityAccess())
            {
                var pose = ResolvePose(entity, transform.ValueRO, alpha, useBandScale, useRenderFrame, renderFrame);
                float phase = PhaseFromEntity(entity);
                float baseOffset = HashToSignedUnit(entity, 53) * StrikeCraftBaseOffset;
                float offset = disableDepthOffset ? 0f : baseOffset + math.sin(time * StrikeCraftFrequency + phase) * StrikeCraftAmplitude;
                float baseScale = DefaultStrikeCraftScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.003f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = pose.Scale * baseScale * scaleMultiplier;
                float3 position = pose.Position + new float3(0f, offset, 0f);
                localToWorld.ValueRW.Value = float4x4.TRS(position, pose.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<ResourcePickupPresentationTag>()
                         .WithEntityAccess())
            {
                var pose = ResolvePose(entity, transform.ValueRO, alpha, useBandScale, useRenderFrame, renderFrame);
                float phase = PhaseFromEntity(entity);
                float baseOffset = HashToSignedUnit(entity, 61) * PickupBaseOffset;
                float offset = disableDepthOffset ? 0f : baseOffset + math.sin(time * PickupFrequency + phase) * PickupAmplitude;
                float baseScale = DefaultPickupScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.004f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = pose.Scale * baseScale * scaleMultiplier;
                float3 position = pose.Position + new float3(0f, offset, 0f);
                localToWorld.ValueRW.Value = float4x4.TRS(position, pose.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<AsteroidPresentationTag, Asteroid>()
                         .WithEntityAccess())
            {
                var pose = ResolvePose(entity, transform.ValueRO, alpha, useBandScale, useRenderFrame, renderFrame);
                float phase = PhaseFromEntity(entity);
                float baseOffset = HashToSignedUnit(entity, 79) * AsteroidBaseOffset;
                float offset = disableDepthOffset ? 0f : baseOffset + math.sin(time * AsteroidFrequency + phase) * AsteroidAmplitude;
                float baseScale = asteroidBaseScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.1f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float jitter = math.lerp(AsteroidJitterMin, AsteroidJitterMax, HashToUnit(entity, 97));
                float scale = pose.Scale * baseScale * jitter * scaleMultiplier;
                float3 position = pose.Position + new float3(0f, offset, 0f);
                localToWorld.ValueRW.Value = float4x4.TRS(position, pose.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<ProjectilePresentationTag>()
                         .WithEntityAccess())
            {
                var pose = ResolvePose(entity, transform.ValueRO, alpha, useBandScale, useRenderFrame, renderFrame);
                float baseScale = DefaultProjectileScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.001f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }
                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = pose.Scale * baseScale * scaleMultiplier;
                localToWorld.ValueRW.Value = float4x4.TRS(pose.Position, pose.Rotation, new float3(scale));
            }

            foreach (var (iconMesh, transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<FleetIconMesh>, RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<FleetImpostorTag>()
                         .WithEntityAccess())
            {
                var pose = ResolvePose(entity, transform.ValueRO, alpha, useBandScale, useRenderFrame, renderFrame);
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
                float scale = pose.Scale * baseScale * scaleMultiplier;
                localToWorld.ValueRW.Value = float4x4.TRS(pose.Position, pose.Rotation, new float3(scale));
            }

            foreach (var (transform, localToWorld, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<LocalToWorld>>()
                         .WithAll<IndividualPresentationTag>()
                         .WithEntityAccess())
            {
                var pose = ResolvePose(entity, transform.ValueRO, alpha, useBandScale, useRenderFrame, renderFrame);
                float baseScale = DefaultIndividualScale;
                if (SystemAPI.HasComponent<PresentationScale>(entity))
                {
                    baseScale = math.max(0.001f, SystemAPI.GetComponentRO<PresentationScale>(entity).ValueRO.Value);
                }

                float scaleMultiplier = ResolveScaleMultiplier(entity);
                float scale = pose.Scale * baseScale * scaleMultiplier;
                localToWorld.ValueRW.Value = float4x4.TRS(pose.Position, pose.Rotation, new float3(scale));
            }
        }

        private struct PoseSample
        {
            public float3 Position;
            public quaternion Rotation;
            public float Scale;
        }

        private PoseSample ResolvePose(
            Entity entity,
            in LocalTransform fallback,
            float alpha,
            bool useBandScale,
            bool useRenderFrame,
            in Space4XRenderFrameState renderFrame)
        {
            if (_poseSnapshotLookup.HasComponent(entity))
            {
                var snapshot = _poseSnapshotLookup[entity];
                if (snapshot.CurrTick == snapshot.PrevTick)
                {
                    var position = ResolveRenderPosition(entity, snapshot.CurrPosition, useBandScale, useRenderFrame, in renderFrame);
                    return new PoseSample
                    {
                        Position = position,
                        Rotation = snapshot.CurrRotation,
                        Scale = snapshot.CurrScale
                    };
                }

                var t = math.saturate(alpha);
                var position = ResolveRenderPosition(entity, math.lerp(snapshot.PrevPosition, snapshot.CurrPosition, t), useBandScale, useRenderFrame, in renderFrame);
                return new PoseSample
                {
                    Position = position,
                    Rotation = math.slerp(snapshot.PrevRotation, snapshot.CurrRotation, t),
                    Scale = math.lerp(snapshot.PrevScale, snapshot.CurrScale, t)
                };
            }

            var fallbackPosition = ResolveRenderPosition(entity, fallback.Position, useBandScale, useRenderFrame, in renderFrame);
            return new PoseSample
            {
                Position = fallbackPosition,
                Rotation = fallback.Rotation,
                Scale = fallback.Scale
            };
        }

        private float3 ResolveRenderPosition(
            Entity entity,
            float3 position,
            bool useBandScale,
            bool useRenderFrame,
            in Space4XRenderFrameState renderFrame)
        {
            if (useRenderFrame)
            {
                if (renderFrame.AnchorFrame == Entity.Null)
                {
                    return position;
                }

                var scale = math.max(0.01f, renderFrame.Scale);
                if (math.abs(scale - 1f) <= 0.0001f)
                {
                    return position;
                }

                return renderFrame.AnchorPosition + (position - renderFrame.AnchorPosition) * scale;
            }

            if (!useBandScale)
            {
                return position;
            }

            if (!_orbitalBandStateLookup.HasComponent(entity))
            {
                return position;
            }

            var band = _orbitalBandStateLookup[entity];
            if (band.InBand == 0 || band.AnchorFrame == Entity.Null)
            {
                return position;
            }

            if (!_frameTransformLookup.HasComponent(band.AnchorFrame))
            {
                return position;
            }

            var anchor = _frameTransformLookup[band.AnchorFrame].PositionWorld;
            var anchorPos = new float3((float)anchor.x, (float)anchor.y, (float)anchor.z);
            var scale = math.max(0.01f, band.PresentationScale);
            if (math.abs(scale - 1f) <= 0.0001f)
            {
                return position;
            }

            return anchorPos + (position - anchorPos) * scale;
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
