using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Math;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Power
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XShipSpecialEnergyBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShipReactorSpec>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var em = state.EntityManager;

            foreach (var (reactor, entity) in SystemAPI.Query<RefRO<ShipReactorSpec>>().WithEntityAccess())
            {
                var config = em.HasComponent<ShipSpecialEnergyConfig>(entity)
                    ? em.GetComponentData<ShipSpecialEnergyConfig>(entity)
                    : ShipSpecialEnergyConfig.Default;

                if (!em.HasComponent<ShipSpecialEnergyConfig>(entity))
                {
                    ecb.AddComponent(entity, config);
                }

                if (!em.HasComponent<ShipSpecialEnergyState>(entity))
                {
                    var baseMax = ResolveBaseMax(reactor.ValueRO, config);
                    var baseRegen = ResolveBaseRegen(reactor.ValueRO, config);
                    ecb.AddComponent(entity, new ShipSpecialEnergyState
                    {
                        Current = baseMax,
                        EffectiveMax = baseMax,
                        EffectiveRegenPerSecond = baseRegen,
                        LastSpent = 0f,
                        LastSpendTick = 0,
                        FailedSpendAttempts = 0,
                        LastUpdatedTick = 0
                    });
                }

                if (!em.HasBuffer<ShipSpecialEnergyPassiveModifier>(entity))
                {
                    ecb.AddBuffer<ShipSpecialEnergyPassiveModifier>(entity);
                }

                if (!em.HasBuffer<ShipSpecialEnergySpendRequest>(entity))
                {
                    ecb.AddBuffer<ShipSpecialEnergySpendRequest>(entity);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static float ResolveBaseMax(in ShipReactorSpec reactor, in ShipSpecialEnergyConfig config)
        {
            var output = math.max(0f, reactor.OutputMW);
            var outputToMax = math.max(0f, config.ReactorOutputToMax);
            return math.max(0f, config.BaseMax + output * outputToMax);
        }

        private static float ResolveBaseRegen(in ShipReactorSpec reactor, in ShipSpecialEnergyConfig config)
        {
            var output = math.max(0f, reactor.OutputMW);
            var outputToRegen = math.max(0f, config.ReactorOutputToRegen);
            var regen = math.max(0f, config.BaseRegenPerSecond + output * outputToRegen);
            var efficiencyScale = math.max(0f, reactor.Efficiency) * math.max(0f, config.ReactorEfficiencyRegenMultiplier);
            return math.max(0f, regen * efficiencyScale);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(Space4XShipPowerAllocationSyncSystem))]
    public partial struct Space4XShipSpecialEnergySystem : ISystem
    {
        private ComponentLookup<RestartState> _restartLookup;
        private BufferLookup<ShipSpecialEnergyPassiveModifier> _passiveLookup;
        private BufferLookup<ShipSpecialEnergySpendRequest> _spendLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShipSpecialEnergyState>();
            state.RequireForUpdate<ShipSpecialEnergyConfig>();
            state.RequireForUpdate<ShipReactorSpec>();
            state.RequireForUpdate<TimeState>();
            _restartLookup = state.GetComponentLookup<RestartState>(true);
            _passiveLookup = state.GetBufferLookup<ShipSpecialEnergyPassiveModifier>(true);
            _spendLookup = state.GetBufferLookup<ShipSpecialEnergySpendRequest>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var deltaTime = math.max(0f, time.FixedDeltaTime);
            _restartLookup.Update(ref state);
            _passiveLookup.Update(ref state);
            _spendLookup.Update(ref state);

            foreach (var (reactor, config, energy, entity) in
                     SystemAPI.Query<RefRO<ShipReactorSpec>, RefRO<ShipSpecialEnergyConfig>, RefRW<ShipSpecialEnergyState>>().WithEntityAccess())
            {
                var additiveMax = 0f;
                var multiplicativeMax = 1f;
                var additiveRegen = 0f;
                var multiplicativeRegen = 1f;

                if (_passiveLookup.HasBuffer(entity))
                {
                    var modifiers = _passiveLookup[entity];
                    for (var i = 0; i < modifiers.Length; i++)
                    {
                        var modifier = modifiers[i];
                        additiveMax += modifier.AdditiveMax;
                        multiplicativeMax *= modifier.MultiplicativeMax <= 0f ? 1f : modifier.MultiplicativeMax;
                        additiveRegen += modifier.AdditiveRegenPerSecond;
                        multiplicativeRegen *= modifier.MultiplicativeRegen <= 0f ? 1f : modifier.MultiplicativeRegen;
                    }
                }

                var baseMax = ResolveBaseMax(reactor.ValueRO, config.ValueRO);
                var baseRegen = ResolveBaseRegen(reactor.ValueRO, config.ValueRO);

                if (_restartLookup.HasComponent(entity))
                {
                    var restart = _restartLookup[entity];
                    if (restart.RestartTimer > 0.01f)
                    {
                        baseRegen *= math.clamp(config.ValueRO.RestartRegenPenaltyMultiplier, 0f, 1f);
                    }
                }

                var effectiveMax = ResourcePoolMath.ResolveModifiedMax(baseMax, additiveMax, multiplicativeMax);
                var effectiveRegen = ResourcePoolMath.ResolveModifiedRate(baseRegen, additiveRegen, multiplicativeRegen);
                var current = ResourcePoolMath.ClampCurrent(energy.ValueRO.Current, effectiveMax);
                var spentThisTick = energy.ValueRO.LastSpendTick == time.Tick
                    ? math.max(0f, energy.ValueRO.LastSpent)
                    : 0f;
                var failedAttempts = energy.ValueRO.FailedSpendAttempts;

                if (_spendLookup.HasBuffer(entity))
                {
                    var requests = _spendLookup[entity];
                    var activationCostScale = math.max(0.05f, config.ValueRO.ActivationCostMultiplier);
                    for (var i = 0; i < requests.Length; i++)
                    {
                        var requestedAmount = math.max(0f, requests[i].Amount) * activationCostScale;
                        if (ResourcePoolMath.TrySpend(ref current, requestedAmount))
                        {
                            spentThisTick += requestedAmount;
                        }
                        else if (requestedAmount > 0f)
                        {
                            failedAttempts = (ushort)math.min((int)ushort.MaxValue, failedAttempts + 1);
                        }
                    }

                    requests.Clear();
                }

                current = ResourcePoolMath.Regen(current, effectiveMax, effectiveRegen, deltaTime);
                energy.ValueRW.Current = current;
                energy.ValueRW.EffectiveMax = effectiveMax;
                energy.ValueRW.EffectiveRegenPerSecond = effectiveRegen;
                energy.ValueRW.LastSpent = spentThisTick;
                energy.ValueRW.LastSpendTick = spentThisTick > 0f ? time.Tick : energy.ValueRO.LastSpendTick;
                energy.ValueRW.FailedSpendAttempts = failedAttempts;
                energy.ValueRW.LastUpdatedTick = time.Tick;
            }
        }

        private static float ResolveBaseMax(in ShipReactorSpec reactor, in ShipSpecialEnergyConfig config)
        {
            var output = math.max(0f, reactor.OutputMW);
            var outputToMax = math.max(0f, config.ReactorOutputToMax);
            return math.max(0f, config.BaseMax + output * outputToMax);
        }

        private static float ResolveBaseRegen(in ShipReactorSpec reactor, in ShipSpecialEnergyConfig config)
        {
            var output = math.max(0f, reactor.OutputMW);
            var outputToRegen = math.max(0f, config.ReactorOutputToRegen);
            var regen = math.max(0f, config.BaseRegenPerSecond + output * outputToRegen);
            var efficiencyScale = math.max(0f, reactor.Efficiency) * math.max(0f, config.ReactorEfficiencyRegenMultiplier);
            return math.max(0f, regen * efficiencyScale);
        }
    }
}
