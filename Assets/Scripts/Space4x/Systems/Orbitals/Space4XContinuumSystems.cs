using PureDOTS.Runtime.Components;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Orbitals
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XReferenceFrameScenarioBootstrapSystem))]
    public partial struct Space4XContinuumConfigBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XContinuumConfig>(out _))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, Space4XContinuumConfig.Default);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XFrameMembershipBootstrapSystem))]
    public partial struct Space4XContinuumStateBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            state.RequireForUpdate<Space4XContinuumConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var missingState = SystemAPI.QueryBuilder()
                .WithAll<Space4XFrameMembership>()
                .WithNone<Space4XContinuumState>()
                .Build();
            if (missingState.IsEmptyIgnoreFilter)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ecb.AddComponent(missingState, new Space4XContinuumState
            {
                AnchorPlanetFrame = Entity.Null,
                Tier = Space4XContinuumTier.Unknown,
                RadiusRatio = 0f,
                DistanceToPlanetCenter = 0f,
                PlanetRadius = 0f,
                LastTransitionTick = 0u,
                TransitionCount = 0u
            });
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFrameTransformSystem))]
    [UpdateAfter(typeof(Space4XFrameMembershipSyncSystem))]
    public partial struct Space4XContinuumTierClassificationSystem : ISystem
    {
        private ComponentLookup<Space4XFrameTransform> _frameTransformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            state.RequireForUpdate<Space4XContinuumConfig>();
            state.RequireForUpdate<TimeState>();
            _frameTransformLookup = state.GetComponentLookup<Space4XFrameTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var referenceFrameConfig = SystemAPI.GetSingleton<Space4XReferenceFrameConfig>();
            var continuumConfig = SystemAPI.GetSingleton<Space4XContinuumConfig>();
            var timeState = SystemAPI.GetSingleton<TimeState>();
            _frameTransformLookup.Update(ref state);

            var defaultRadius = math.max(1f, continuumConfig.DefaultPlanetRadius);

            if (referenceFrameConfig.Enabled == 0 || continuumConfig.Enabled == 0)
            {
                foreach (var continuumState in SystemAPI.Query<RefRW<Space4XContinuumState>>())
                {
                    continuumState.ValueRW.AnchorPlanetFrame = Entity.Null;
                    continuumState.ValueRW.Tier = Space4XContinuumTier.Unknown;
                    continuumState.ValueRW.RadiusRatio = 0f;
                    continuumState.ValueRW.DistanceToPlanetCenter = 0f;
                    continuumState.ValueRW.PlanetRadius = defaultRadius;
                }
                return;
            }

            var planetFrames = new NativeList<PlanetFrameInfo>(Allocator.Temp);
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<Space4XFrameTransform>>()
                         .WithAll<Space4XReferenceFramePlanetTag>()
                         .WithEntityAccess())
            {
                planetFrames.Add(new PlanetFrameInfo
                {
                    Frame = entity,
                    PositionWorld = transform.ValueRO.PositionWorld
                });
            }

            if (planetFrames.Length == 0)
            {
                planetFrames.Dispose();
                return;
            }

            var hysteresis = math.max(0f, continuumConfig.HysteresisRatio);

            foreach (var (membership, continuumState) in SystemAPI.Query<RefRO<Space4XFrameMembership>, RefRW<Space4XContinuumState>>())
            {
                var frame = membership.ValueRO.Frame;
                if (frame == Entity.Null || !_frameTransformLookup.HasComponent(frame))
                {
                    continuumState.ValueRW.AnchorPlanetFrame = Entity.Null;
                    continuumState.ValueRW.Tier = Space4XContinuumTier.Unknown;
                    continuumState.ValueRW.RadiusRatio = 0f;
                    continuumState.ValueRW.DistanceToPlanetCenter = 0f;
                    continuumState.ValueRW.PlanetRadius = defaultRadius;
                    continue;
                }

                var frameTransform = _frameTransformLookup[frame];
                var worldPosition = frameTransform.PositionWorld + new double3(
                    membership.ValueRO.LocalPosition.x,
                    membership.ValueRO.LocalPosition.y,
                    membership.ValueRO.LocalPosition.z);

                var bestPlanet = planetFrames[0];
                var bestDistanceSq = math.lengthsq(worldPosition - bestPlanet.PositionWorld);
                for (var i = 1; i < planetFrames.Length; i++)
                {
                    var candidate = planetFrames[i];
                    var distanceSq = math.lengthsq(worldPosition - candidate.PositionWorld);
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        bestPlanet = candidate;
                    }
                }

                var distanceToPlanet = (float)math.sqrt(bestDistanceSq);
                var radiusRatio = distanceToPlanet / defaultRadius;
                var previousTier = continuumState.ValueRO.Tier;
                var nextTier = ResolveTier(radiusRatio, previousTier, hysteresis, in continuumConfig);

                continuumState.ValueRW.AnchorPlanetFrame = bestPlanet.Frame;
                continuumState.ValueRW.Tier = nextTier;
                continuumState.ValueRW.RadiusRatio = radiusRatio;
                continuumState.ValueRW.DistanceToPlanetCenter = distanceToPlanet;
                continuumState.ValueRW.PlanetRadius = defaultRadius;
                if (nextTier != previousTier)
                {
                    continuumState.ValueRW.LastTransitionTick = timeState.Tick;
                    continuumState.ValueRW.TransitionCount = continuumState.ValueRO.TransitionCount + 1u;
                }
            }

            planetFrames.Dispose();
        }

        private static Space4XContinuumTier ResolveTier(
            float ratio,
            Space4XContinuumTier previous,
            float hysteresis,
            in Space4XContinuumConfig config)
        {
            var surface = math.max(0f, config.SurfaceLocalMinRatio);
            var approach = math.max(surface, config.ApproachShellMinRatio);
            var nearOrbital = math.max(approach, config.NearOrbitalMinRatio);
            var operational = math.max(nearOrbital, config.OperationalOrbitMinRatio);
            var deep = math.max(operational, config.DeepOrbitMinRatio);

            var baseTier = ResolveTierNoHysteresis(ratio, surface, approach, nearOrbital, operational, deep);
            if (previous == Space4XContinuumTier.Unknown)
            {
                return baseTier;
            }

            GetTierRange(previous, surface, approach, nearOrbital, operational, deep, out var lower, out var upper);
            if (ratio >= math.max(0f, lower - hysteresis) && ratio < upper + hysteresis)
            {
                return previous;
            }

            return baseTier;
        }

        private static Space4XContinuumTier ResolveTierNoHysteresis(
            float ratio,
            float surface,
            float approach,
            float nearOrbital,
            float operational,
            float deep)
        {
            if (ratio >= deep)
            {
                return Space4XContinuumTier.DeepOrbit;
            }

            if (ratio >= operational)
            {
                return Space4XContinuumTier.OperationalOrbit;
            }

            if (ratio >= nearOrbital)
            {
                return Space4XContinuumTier.NearOrbitalCombat;
            }

            if (ratio >= approach)
            {
                return Space4XContinuumTier.ApproachShell;
            }

            if (ratio >= surface)
            {
                return Space4XContinuumTier.SurfaceLocal;
            }

            return Space4XContinuumTier.SurfaceLocal;
        }

        private static void GetTierRange(
            Space4XContinuumTier tier,
            float surface,
            float approach,
            float nearOrbital,
            float operational,
            float deep,
            out float lower,
            out float upper)
        {
            switch (tier)
            {
                case Space4XContinuumTier.DeepOrbit:
                    lower = deep;
                    upper = float.MaxValue;
                    return;
                case Space4XContinuumTier.OperationalOrbit:
                    lower = operational;
                    upper = deep;
                    return;
                case Space4XContinuumTier.NearOrbitalCombat:
                    lower = nearOrbital;
                    upper = operational;
                    return;
                case Space4XContinuumTier.ApproachShell:
                    lower = approach;
                    upper = nearOrbital;
                    return;
                case Space4XContinuumTier.SurfaceLocal:
                    lower = surface;
                    upper = approach;
                    return;
                default:
                    lower = 0f;
                    upper = float.MaxValue;
                    return;
            }
        }

        private struct PlanetFrameInfo
        {
            public Entity Frame;
            public double3 PositionWorld;
        }
    }
}
