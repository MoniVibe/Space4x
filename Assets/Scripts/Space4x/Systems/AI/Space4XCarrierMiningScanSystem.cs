using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TimeState = PureDOTS.Runtime.Components.TimeState;
using RewindState = PureDOTS.Runtime.Components.RewindState;
using RewindMode = PureDOTS.Runtime.Components.RewindMode;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Selects mining targets for carriers so they reposition between deposits and seed miner orders.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    [UpdateBefore(typeof(Space4XMiningOrderAutoAssignSystem))]
    public partial struct Space4XCarrierMiningScanSystem : ISystem
    {
        private const float ScanIntervalSeconds = 6f;

        private ComponentLookup<global::Space4X.Registry.ResourceSourceState> _resourceStateLookup;
        private ComponentLookup<global::Space4X.Registry.ResourceSourceConfig> _resourceConfigLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<global::Space4X.Registry.CarrierMiningTarget> _miningTargetLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _resourceStateLookup = state.GetComponentLookup<global::Space4X.Registry.ResourceSourceState>(true);
            _resourceConfigLookup = state.GetComponentLookup<global::Space4X.Registry.ResourceSourceConfig>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _miningTargetLookup = state.GetComponentLookup<global::Space4X.Registry.CarrierMiningTarget>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _resourceStateLookup.Update(ref state);
            _resourceConfigLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _miningTargetLookup.Update(ref state);

            var vesselCount = SystemAPI.QueryBuilder().WithAll<global::Space4X.Registry.MiningVessel>().Build().CalculateEntityCount();
            var miningCarriers = new NativeHashSet<Entity>(math.max(1, vesselCount), Allocator.Temp);
            var hasMiningCarriers = false;
            foreach (var vessel in SystemAPI.Query<RefRO<global::Space4X.Registry.MiningVessel>>())
            {
                var carrierEntity = vessel.ValueRO.CarrierEntity;
                if (carrierEntity == Entity.Null)
                {
                    continue;
                }

                if (miningCarriers.Add(carrierEntity))
                {
                    hasMiningCarriers = true;
                }
            }

            if (!hasMiningCarriers)
            {
                miningCarriers.Dispose();
                return;
            }

            var resources = new NativeList<ResourceCandidate>(Allocator.Temp);
            foreach (var (resourceState, transform, entity) in SystemAPI
                         .Query<RefRO<global::Space4X.Registry.ResourceSourceState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (resourceState.ValueRO.UnitsRemaining <= 0f)
                {
                    continue;
                }

                var gatherRate = 1f;
                if (_resourceConfigLookup.HasComponent(entity))
                {
                    gatherRate = math.max(0.1f, _resourceConfigLookup[entity].GatherRatePerWorker);
                }

                resources.Add(new ResourceCandidate
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    UnitsRemaining = resourceState.ValueRO.UnitsRemaining,
                    GatherRate = gatherRate
                });
            }

            if (resources.Length == 0)
            {
                resources.Dispose();
                miningCarriers.Dispose();
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<global::Space4X.Registry.Carrier>()
                         .WithEntityAccess())
            {
                if (!miningCarriers.Contains(entity))
                {
                    continue;
                }

                if (_miningTargetLookup.HasComponent(entity))
                {
                    continue;
                }

                ecb.AddComponent(entity, new global::Space4X.Registry.CarrierMiningTarget
                {
                    TargetEntity = Entity.Null,
                    TargetPosition = transform.ValueRO.Position,
                    AssignedTick = 0,
                    NextScanTick = timeState.Tick
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            _miningTargetLookup.Update(ref state);

            var fixedDt = math.max(1e-6f, timeState.FixedDeltaTime);
            var scanIntervalTicks = (uint)math.max(1f, math.ceil(ScanIntervalSeconds / fixedDt));

            foreach (var (transform, target, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<global::Space4X.Registry.CarrierMiningTarget>>()
                         .WithAll<global::Space4X.Registry.Carrier>()
                         .WithEntityAccess())
            {
                if (!miningCarriers.Contains(entity))
                {
                    continue;
                }

                if (!ShouldRescan(target.ValueRO, timeState.Tick, ref _resourceStateLookup))
                {
                    continue;
                }

                var bestScore = float.MinValue;
                var bestCandidate = default(ResourceCandidate);

                for (var i = 0; i < resources.Length; i++)
                {
                    var candidate = resources[i];
                    var distanceSq = math.distancesq(transform.ValueRO.Position, candidate.Position);
                    var distance = math.max(1f, math.sqrt(distanceSq));
                    var score = (candidate.UnitsRemaining * candidate.GatherRate) / distance;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCandidate = candidate;
                    }
                }

                if (bestCandidate.Entity != Entity.Null)
                {
                    target.ValueRW.TargetEntity = bestCandidate.Entity;
                    target.ValueRW.TargetPosition = bestCandidate.Position;
                    target.ValueRW.AssignedTick = timeState.Tick;
                    target.ValueRW.NextScanTick = timeState.Tick + scanIntervalTicks;
                }
                else
                {
                    target.ValueRW.TargetEntity = Entity.Null;
                    target.ValueRW.TargetPosition = transform.ValueRO.Position;
                    target.ValueRW.NextScanTick = timeState.Tick + scanIntervalTicks;
                }
            }

            resources.Dispose();
            miningCarriers.Dispose();
        }

        private static bool ShouldRescan(in global::Space4X.Registry.CarrierMiningTarget target, uint currentTick, ref ComponentLookup<global::Space4X.Registry.ResourceSourceState> resourceStateLookup)
        {
            if (target.TargetEntity != Entity.Null &&
                resourceStateLookup.HasComponent(target.TargetEntity) &&
                resourceStateLookup[target.TargetEntity].UnitsRemaining > 0f &&
                currentTick < target.NextScanTick)
            {
                return false;
            }

            return currentTick >= target.NextScanTick || target.TargetEntity == Entity.Null;
        }

        private struct ResourceCandidate
        {
            public Entity Entity;
            public float3 Position;
            public float UnitsRemaining;
            public float GatherRate;
        }
    }
}
