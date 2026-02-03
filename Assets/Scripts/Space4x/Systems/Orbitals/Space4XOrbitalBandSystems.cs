using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Orbitals
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XReferenceFrameScenarioBootstrapSystem))]
    public partial struct Space4XOrbitalBandBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            state.RequireForUpdate<Space4XOrbitalBandConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XOrbitalBandConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (frame, entity) in SystemAPI.Query<RefRO<Space4XReferenceFrame>>()
                         .WithAny<Space4XReferenceFramePlanetTag, Space4XOrbitalBandAnchorTag>()
                         .WithEntityAccess())
            {
                if (SystemAPI.HasComponent<Space4XOrbitalBandRegion>(entity))
                {
                    continue;
                }

                var outerRadius = math.max(0f, config.OuterRadius);
                if (outerRadius <= 0f && SystemAPI.HasComponent<Space4XSOIRegion>(entity))
                {
                    var soi = SystemAPI.GetComponent<Space4XSOIRegion>(entity);
                    outerRadius = (float)math.max(0.0, soi.EnterRadius);
                }

                if (outerRadius <= 0f)
                {
                    continue;
                }

                var innerRadius = math.max(0f, config.InnerRadius);
                if (innerRadius <= 0f)
                {
                    innerRadius = math.max(5f, outerRadius * 0.15f);
                }

                var region = new Space4XOrbitalBandRegion
                {
                    InnerRadius = innerRadius,
                    OuterRadius = math.max(innerRadius, outerRadius),
                    DistanceScale = math.max(0.01f, config.DistanceScale),
                    SpeedScale = math.max(0.01f, config.SpeedScale),
                    RangeScale = math.max(0.01f, config.RangeScale)
                };

                ecb.AddComponent(entity, region);

                if (!SystemAPI.HasComponent<Space4XOrbitalBandAnchorTag>(entity))
                {
                    ecb.AddComponent<Space4XOrbitalBandAnchorTag>(entity);
                }
            }

            var missingBandState = SystemAPI.QueryBuilder()
                .WithAll<VesselMovement>()
                .WithNone<Space4XOrbitalBandState>()
                .Build();
            if (!missingBandState.IsEmptyIgnoreFilter)
            {
                ecb.AddComponent(missingBandState, new Space4XOrbitalBandState
                {
                    AnchorFrame = Entity.Null,
                    DistanceScale = 1f,
                    SpeedScale = 1f,
                    RangeScale = 1f,
                    InBand = 0
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFrameMembershipSyncSystem))]
    public partial struct Space4XOrbitalBandStateSystem : ISystem
    {
        private ComponentLookup<Space4XOrbitalBandRegion> _bandLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            state.RequireForUpdate<Space4XOrbitalBandConfig>();
            _bandLookup = state.GetComponentLookup<Space4XOrbitalBandRegion>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XOrbitalBandConfig>();
            _bandLookup.Update(ref state);
            var enterMultiplier = math.max(0.1f, config.EnterMultiplier);
            var exitMultiplier = math.max(enterMultiplier, config.ExitMultiplier);

            foreach (var (membership, bandState) in SystemAPI
                         .Query<RefRO<Space4XFrameMembership>, RefRW<Space4XOrbitalBandState>>())
            {
                if (config.Enabled == 0)
                {
                    bandState.ValueRW.AnchorFrame = Entity.Null;
                    bandState.ValueRW.DistanceScale = 1f;
                    bandState.ValueRW.SpeedScale = 1f;
                    bandState.ValueRW.RangeScale = 1f;
                    bandState.ValueRW.InBand = 0;
                    continue;
                }

                var frame = membership.ValueRO.Frame;
                if (frame == Entity.Null || !_bandLookup.HasComponent(frame))
                {
                    bandState.ValueRW.AnchorFrame = Entity.Null;
                    bandState.ValueRW.DistanceScale = 1f;
                    bandState.ValueRW.SpeedScale = 1f;
                    bandState.ValueRW.RangeScale = 1f;
                    bandState.ValueRW.InBand = 0;
                    continue;
                }

                var region = _bandLookup[frame];
                var inner = math.max(0f, region.InnerRadius);
                var outer = math.max(inner, region.OuterRadius);
                var distance = math.length(membership.ValueRO.LocalPosition);

                var inBand = bandState.ValueRO.InBand != 0;
                var enterInner = inner * enterMultiplier;
                var enterOuter = outer * enterMultiplier;
                var exitInner = inner * exitMultiplier;
                var exitOuter = outer * exitMultiplier;
                var nextInBand = inBand
                    ? distance >= exitInner && distance <= exitOuter
                    : distance >= enterInner && distance <= enterOuter;

                if (nextInBand)
                {
                    bandState.ValueRW.AnchorFrame = frame;
                    bandState.ValueRW.DistanceScale = math.max(0.01f, region.DistanceScale);
                    bandState.ValueRW.SpeedScale = math.max(0.01f, region.SpeedScale);
                    bandState.ValueRW.RangeScale = math.max(0.01f, region.RangeScale);
                    bandState.ValueRW.InBand = 1;
                }
                else
                {
                    bandState.ValueRW.AnchorFrame = Entity.Null;
                    bandState.ValueRW.DistanceScale = 1f;
                    bandState.ValueRW.SpeedScale = 1f;
                    bandState.ValueRW.RangeScale = 1f;
                    bandState.ValueRW.InBand = 0;
                }
            }
        }
    }
}
