using PureDOTS.Runtime;
using PureDOTS.Runtime.Economy.Production;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Derives research unlock tiers from TechLevel and applies them to production,
    /// training, mining efficiency, and salvage capabilities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XTechDiffusionSystem))]
    [UpdateBefore(typeof(Space4XFacilityAutoProductionSystem))]
    public partial struct Space4XResearchUnlockSystem : ISystem
    {
        private ComponentLookup<Space4XColony> _colonyLookup;
        private ComponentLookup<TechLevel> _techLookup;
        private ComponentLookup<Space4XResearchUnlocks> _unlockLookup;
        private ComponentLookup<ColonyFacilityLink> _facilityLinkLookup;
        private ComponentLookup<ColonyTechLink> _colonyTechLinkLookup;
        private ComponentLookup<ProductionQueueCapacity> _queueCapacityLookup;
        private ComponentLookup<CrewReservePolicy> _reservePolicyLookup;
        private ComponentLookup<CrewReservePolicyBaseline> _reserveBaselineLookup;
        private ComponentLookup<CrewTrainingState> _trainingStateLookup;
        private ComponentLookup<CrewTrainingStateBaseline> _trainingBaselineLookup;
        private ComponentLookup<SalvageCapable> _salvageLookup;
        private ComponentLookup<SalvageCapabilityBaseline> _salvageBaselineLookup;
        private ComponentLookup<MiningVessel> _miningVesselLookup;
        private ComponentLookup<MiningEfficiencyBaseline> _miningBaselineLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<RewindState>();
            _colonyLookup = state.GetComponentLookup<Space4XColony>(true);
            _techLookup = state.GetComponentLookup<TechLevel>(true);
            _unlockLookup = state.GetComponentLookup<Space4XResearchUnlocks>(false);
            _facilityLinkLookup = state.GetComponentLookup<ColonyFacilityLink>(true);
            _colonyTechLinkLookup = state.GetComponentLookup<ColonyTechLink>(true);
            _queueCapacityLookup = state.GetComponentLookup<ProductionQueueCapacity>(false);
            _reservePolicyLookup = state.GetComponentLookup<CrewReservePolicy>(false);
            _reserveBaselineLookup = state.GetComponentLookup<CrewReservePolicyBaseline>(true);
            _trainingStateLookup = state.GetComponentLookup<CrewTrainingState>(false);
            _trainingBaselineLookup = state.GetComponentLookup<CrewTrainingStateBaseline>(true);
            _salvageLookup = state.GetComponentLookup<SalvageCapable>(false);
            _salvageBaselineLookup = state.GetComponentLookup<SalvageCapabilityBaseline>(true);
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(false);
            _miningBaselineLookup = state.GetComponentLookup<MiningEfficiencyBaseline>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out RewindState rewind) ||
                rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _colonyLookup.Update(ref state);
            _techLookup.Update(ref state);
            _unlockLookup.Update(ref state);
            _facilityLinkLookup.Update(ref state);
            _colonyTechLinkLookup.Update(ref state);
            _queueCapacityLookup.Update(ref state);
            _reservePolicyLookup.Update(ref state);
            _reserveBaselineLookup.Update(ref state);
            _trainingStateLookup.Update(ref state);
            _trainingBaselineLookup.Update(ref state);
            _salvageLookup.Update(ref state);
            _salvageBaselineLookup.Update(ref state);
            _miningVesselLookup.Update(ref state);
            _miningBaselineLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (tech, entity) in SystemAPI.Query<RefRO<TechLevel>>().WithAll<Space4XColony>().WithEntityAccess())
            {
                var unlocks = Space4XResearchUnlocks.FromTech(tech.ValueRO);
                if (_unlockLookup.HasComponent(entity))
                {
                    _unlockLookup[entity] = unlocks;
                }
                else
                {
                    ecb.AddComponent(entity, unlocks);
                }
            }

            foreach (var (link, facility) in SystemAPI.Query<RefRO<ColonyFacilityLink>>().WithEntityAccess())
            {
                var colony = link.ValueRO.Colony;
                if (colony == Entity.Null || !_unlockLookup.HasComponent(colony))
                {
                    continue;
                }

                var unlocks = _unlockLookup[colony];
                var capacity = new ProductionQueueCapacity
                {
                    MaxQueuedJobs = unlocks.ProductionQueueSlots
                };

                if (_queueCapacityLookup.HasComponent(facility))
                {
                    _queueCapacityLookup[facility] = capacity;
                }
                else
                {
                    ecb.AddComponent(facility, capacity);
                }
            }

            foreach (var (policy, entity) in SystemAPI.Query<RefRW<CrewReservePolicy>>().WithEntityAccess())
            {
                if (!TryResolveColony(entity, out var colony) || !_unlockLookup.HasComponent(colony))
                {
                    continue;
                }

                var unlocks = _unlockLookup[colony];
                var baseline = _reserveBaselineLookup.HasComponent(entity)
                    ? _reserveBaselineLookup[entity]
                    : new CrewReservePolicyBaseline
                    {
                        TrainingRatePerTick = policy.ValueRO.TrainingRatePerTick,
                        MinTraining = policy.ValueRO.MinTraining,
                        MaxTraining = policy.ValueRO.MaxTraining
                    };

                if (!_reserveBaselineLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, baseline);
                }

                policy.ValueRW.TrainingRatePerTick = baseline.TrainingRatePerTick * unlocks.TrainingScalar;
                policy.ValueRW.MaxTraining = math.clamp(baseline.MaxTraining + unlocks.BionicsBonus, baseline.MaxTraining, 1f);
                policy.ValueRW.MinTraining = math.min(policy.ValueRW.MaxTraining, baseline.MinTraining);
            }

            foreach (var (training, entity) in SystemAPI.Query<RefRW<CrewTrainingState>>().WithEntityAccess())
            {
                if (!TryResolveColony(entity, out var colony) || !_unlockLookup.HasComponent(colony))
                {
                    continue;
                }

                var unlocks = _unlockLookup[colony];
                var baseline = _trainingBaselineLookup.HasComponent(entity)
                    ? _trainingBaselineLookup[entity]
                    : new CrewTrainingStateBaseline
                    {
                        TrainingRatePerTick = training.ValueRO.TrainingRatePerTick,
                        MaxTraining = training.ValueRO.MaxTraining
                    };

                if (!_trainingBaselineLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, baseline);
                }

                training.ValueRW.TrainingRatePerTick = baseline.TrainingRatePerTick * unlocks.TrainingScalar;
                training.ValueRW.MaxTraining = math.clamp(baseline.MaxTraining + unlocks.BionicsBonus, baseline.MaxTraining, 1f);
            }

            foreach (var (capable, entity) in SystemAPI.Query<RefRW<SalvageCapable>>().WithEntityAccess())
            {
                if (!TryResolveColony(entity, out var colony) || !_unlockLookup.HasComponent(colony))
                {
                    continue;
                }

                var unlocks = _unlockLookup[colony];
                var baseline = _salvageBaselineLookup.HasComponent(entity)
                    ? _salvageBaselineLookup[entity]
                    : new SalvageCapabilityBaseline
                    {
                        SpeedBonus = capable.ValueRO.SpeedBonus,
                        RiskReduction = capable.ValueRO.RiskReduction,
                        YieldBonus = capable.ValueRO.YieldBonus,
                        CanReactivate = capable.ValueRO.CanReactivate
                    };

                if (!_salvageBaselineLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, baseline);
                }

                var speedBonus = (float)baseline.SpeedBonus + unlocks.SalvageTier * 0.05f;
                var riskReduction = (float)baseline.RiskReduction + unlocks.SalvageTier * 0.03f;
                var yieldBonus = (float)baseline.YieldBonus + unlocks.SalvageTier * 0.04f;

                capable.ValueRW.SpeedBonus = (half)math.clamp(speedBonus, 0f, 2f);
                capable.ValueRW.RiskReduction = (half)math.clamp(riskReduction, 0f, 0.9f);
                capable.ValueRW.YieldBonus = (half)math.clamp(yieldBonus, 0f, 1f);
                capable.ValueRW.CanReactivate = (byte)((baseline.CanReactivate != 0 || unlocks.SalvageTier >= 2) ? 1 : 0);
            }

            foreach (var (vessel, entity) in SystemAPI.Query<RefRW<MiningVessel>>().WithEntityAccess())
            {
                if (!TryResolveColony(entity, out var colony) || !_unlockLookup.HasComponent(colony))
                {
                    continue;
                }

                var unlocks = _unlockLookup[colony];
                var baseline = _miningBaselineLookup.HasComponent(entity)
                    ? _miningBaselineLookup[entity]
                    : new MiningEfficiencyBaseline { Value = vessel.ValueRO.MiningEfficiency };

                if (!_miningBaselineLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, baseline);
                }

                vessel.ValueRW.MiningEfficiency = baseline.Value * unlocks.ExtractionScalar;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private bool TryResolveColony(Entity entity, out Entity colony)
        {
            if (_colonyLookup.HasComponent(entity))
            {
                colony = entity;
                return true;
            }

            if (_facilityLinkLookup.HasComponent(entity))
            {
                colony = _facilityLinkLookup[entity].Colony;
                return colony != Entity.Null;
            }

            if (_colonyTechLinkLookup.HasComponent(entity))
            {
                colony = _colonyTechLinkLookup[entity].Colony;
                return colony != Entity.Null;
            }

            colony = Entity.Null;
            return false;
        }
    }
}
