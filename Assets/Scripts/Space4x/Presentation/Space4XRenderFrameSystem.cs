using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Presentation.Camera;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    public enum Space4XRenderFrameTier : byte
    {
        Surface = 0,
        Orbital = 1,
        Deep = 2
    }

    public struct Space4XRenderFrameConfig : IComponentData
    {
        public byte Enabled;
        public byte UseBandScale;
        public ushort Reserved0;
        public float SurfaceScale;
        public float OrbitalScale;
        public float DeepScale;
        public float SurfaceEnterMultiplier;
        public float SurfaceExitMultiplier;
        public float OrbitalEnterMultiplier;
        public float OrbitalExitMultiplier;
        public uint MinHoldTicks;

        public static Space4XRenderFrameConfig Default => new Space4XRenderFrameConfig
        {
            Enabled = 1,
            UseBandScale = 1,
            SurfaceScale = 1f,
            OrbitalScale = 0.75f,
            DeepScale = 0.25f,
            SurfaceEnterMultiplier = 0.95f,
            SurfaceExitMultiplier = 1.05f,
            OrbitalEnterMultiplier = 0.95f,
            OrbitalExitMultiplier = 1.05f,
            MinHoldTicks = 30
        };
    }

    public struct Space4XRenderFrameState : IComponentData
    {
        public Entity AnchorFrame;
        public float3 AnchorPosition;
        public float Scale;
        public Space4XRenderFrameTier Tier;
        public uint LastSwitchTick;
    }

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XRenderFrameResolveSystem : ISystem
    {
        private ComponentLookup<Space4XFrameTransform> _frameTransformLookup;
        private ComponentLookup<Space4XOrbitalBandRegion> _bandRegionLookup;
        private ComponentLookup<Space4XSOIRegion> _soiRegionLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XCameraState>();
            state.RequireForUpdate<TimeState>();
            _frameTransformLookup = state.GetComponentLookup<Space4XFrameTransform>(true);
            _bandRegionLookup = state.GetComponentLookup<Space4XOrbitalBandRegion>(true);
            _soiRegionLookup = state.GetComponentLookup<Space4XSOIRegion>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<Space4XCameraState>(out var cameraState))
            {
                return;
            }

            var config = EnsureConfig(ref state);
            if (config.Enabled == 0)
            {
                return;
            }

            _frameTransformLookup.Update(ref state);
            _bandRegionLookup.Update(ref state);
            _soiRegionLookup.Update(ref state);

            var focus = cameraState.FocusPoint;
            if (math.lengthsq(focus) <= 0.0001f && math.lengthsq(cameraState.Position) > 0.0001f)
            {
                focus = cameraState.Position;
            }

            var anchorFrame = ResolveAnchorFrame(ref state, focus, out var anchorPosition);
            var stateEntity = EnsureStateEntity(ref state, out var renderState);

            if (anchorFrame == Entity.Null)
            {
                renderState.AnchorFrame = Entity.Null;
                renderState.AnchorPosition = float3.zero;
                renderState.Scale = 1f;
                renderState.Tier = Space4XRenderFrameTier.Deep;
                renderState.LastSwitchTick = 0;
                state.EntityManager.SetComponentData(stateEntity, renderState);
                return;
            }

            ResolveRadii(anchorFrame, out var innerRadius, out var outerRadius);
            float distance = math.distance(focus, anchorPosition);
            var timeState = SystemAPI.GetSingleton<TimeState>();

            var nextTier = ResolveTier(distance, innerRadius, outerRadius, renderState.Tier, config);
            if (nextTier != renderState.Tier && timeState.Tick - renderState.LastSwitchTick < config.MinHoldTicks)
            {
                nextTier = renderState.Tier;
            }

            if (nextTier != renderState.Tier)
            {
                renderState.LastSwitchTick = timeState.Tick;
            }

            var orbitalScale = config.OrbitalScale;
            if (config.UseBandScale != 0 &&
                SystemAPI.TryGetSingleton<Space4XOrbitalBandConfig>(out var bandConfig) &&
                bandConfig.PresentationScale > 0f)
            {
                orbitalScale = bandConfig.PresentationScale;
            }

            renderState.AnchorFrame = anchorFrame;
            renderState.AnchorPosition = anchorPosition;
            renderState.Tier = nextTier;
            renderState.Scale = nextTier switch
            {
                Space4XRenderFrameTier.Surface => math.max(0.01f, config.SurfaceScale),
                Space4XRenderFrameTier.Orbital => math.max(0.01f, orbitalScale),
                _ => math.max(0.01f, config.DeepScale)
            };

            state.EntityManager.SetComponentData(stateEntity, renderState);
        }

        private Space4XRenderFrameConfig EnsureConfig(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<Space4XRenderFrameConfig>(out var config))
            {
                return config;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XRenderFrameConfig));
            var defaults = Space4XRenderFrameConfig.Default;
            state.EntityManager.SetComponentData(entity, defaults);
            state.EntityManager.SetName(entity, "Space4XRenderFrameConfig");
            return defaults;
        }

        private Entity EnsureStateEntity(ref SystemState state, out Space4XRenderFrameState renderState)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XRenderFrameState>(out var entity))
            {
                renderState = SystemAPI.GetSingleton<Space4XRenderFrameState>();
                return entity;
            }

            entity = state.EntityManager.CreateEntity(typeof(Space4XRenderFrameState));
            renderState = new Space4XRenderFrameState
            {
                AnchorFrame = Entity.Null,
                AnchorPosition = float3.zero,
                Scale = 1f,
                Tier = Space4XRenderFrameTier.Deep,
                LastSwitchTick = 0
            };
            state.EntityManager.SetComponentData(entity, renderState);
            state.EntityManager.SetName(entity, "Space4XRenderFrameState");
            return entity;
        }

        private Entity ResolveAnchorFrame(ref SystemState state, float3 focus, out float3 anchorPosition)
        {
            Entity best = Entity.Null;
            anchorPosition = float3.zero;
            float bestDistanceSq = float.MaxValue;

            foreach (var (frameTransform, entity) in SystemAPI
                         .Query<RefRO<Space4XFrameTransform>>()
                         .WithAll<Space4XReferenceFramePlanetTag>()
                         .WithEntityAccess())
            {
                var world = frameTransform.ValueRO.PositionWorld;
                var pos = new float3((float)world.x, (float)world.y, (float)world.z);
                float distanceSq = math.lengthsq(focus - pos);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    best = entity;
                    anchorPosition = pos;
                }
            }

            if (best != Entity.Null)
            {
                return best;
            }

            foreach (var (frameTransform, entity) in SystemAPI
                         .Query<RefRO<Space4XFrameTransform>>()
                         .WithAll<Space4XReferenceFrameStarSystemTag>()
                         .WithEntityAccess())
            {
                var world = frameTransform.ValueRO.PositionWorld;
                anchorPosition = new float3((float)world.x, (float)world.y, (float)world.z);
                return entity;
            }

            return Entity.Null;
        }

        private void ResolveRadii(Entity anchorFrame, out float innerRadius, out float outerRadius)
        {
            innerRadius = 0f;
            outerRadius = 0f;

            if (_bandRegionLookup.HasComponent(anchorFrame))
            {
                var region = _bandRegionLookup[anchorFrame];
                innerRadius = math.max(0f, region.InnerRadius);
                outerRadius = math.max(innerRadius, region.OuterRadius);
            }
            else if (_soiRegionLookup.HasComponent(anchorFrame))
            {
                var soi = _soiRegionLookup[anchorFrame];
                outerRadius = (float)math.max(0.0, soi.EnterRadius);
                if (outerRadius > 0f)
                {
                    innerRadius = math.max(5f, outerRadius * 0.15f);
                }
            }

            if (innerRadius <= 0f && outerRadius > 0f)
            {
                innerRadius = math.max(5f, outerRadius * 0.15f);
            }
        }

        private static Space4XRenderFrameTier ResolveTier(
            float distance,
            float innerRadius,
            float outerRadius,
            Space4XRenderFrameTier currentTier,
            in Space4XRenderFrameConfig config)
        {
            if (innerRadius <= 0f || outerRadius <= 0f)
            {
                return Space4XRenderFrameTier.Deep;
            }

            var surfaceEnter = innerRadius * math.max(0.01f, config.SurfaceEnterMultiplier);
            var surfaceExit = innerRadius * math.max(config.SurfaceExitMultiplier, config.SurfaceEnterMultiplier);
            var orbitalEnter = outerRadius * math.max(0.01f, config.OrbitalEnterMultiplier);
            var orbitalExit = outerRadius * math.max(config.OrbitalExitMultiplier, config.OrbitalEnterMultiplier);

            return currentTier switch
            {
                Space4XRenderFrameTier.Surface => distance > surfaceExit
                    ? (distance <= orbitalEnter ? Space4XRenderFrameTier.Orbital : Space4XRenderFrameTier.Deep)
                    : Space4XRenderFrameTier.Surface,
                Space4XRenderFrameTier.Orbital => distance < surfaceEnter
                    ? Space4XRenderFrameTier.Surface
                    : (distance > orbitalExit ? Space4XRenderFrameTier.Deep : Space4XRenderFrameTier.Orbital),
                _ => distance < orbitalEnter ? Space4XRenderFrameTier.Orbital : Space4XRenderFrameTier.Deep
            };
        }
    }
}
