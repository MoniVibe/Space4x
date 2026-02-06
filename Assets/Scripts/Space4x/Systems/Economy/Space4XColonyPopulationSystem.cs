using PureDOTS.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XColonyIndustryFeedSystem))]
    public partial struct Space4XColonyPopulationSystem : ISystem
    {
        private const float DemandToConsumptionScalar = 0.05f;
        private const float GrowthRatePerSecond = 0.00002f;
        private const float DeclineRatePerSecond = 0.00008f;

        private ComponentLookup<ColonyIndustryStock> _stockLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<Space4XColony>();

            _stockLookup = state.GetComponentLookup<ColonyIndustryStock>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime) ||
                tickTime.IsPaused ||
                !tickTime.IsPlaying)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) ||
                rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = math.max(0f, tickTime.FixedDeltaTime * math.max(0.01f, tickTime.CurrentSpeedMultiplier));
            if (deltaTime <= 0f)
            {
                return;
            }

            _stockLookup.Update(ref state);

            foreach (var (colonyRW, entity) in SystemAPI.Query<RefRW<Space4XColony>>().WithEntityAccess())
            {
                var colony = colonyRW.ValueRO;
                var population = math.max(0f, colony.Population);
                if (population <= 0f)
                {
                    colonyRW.ValueRW.Population = 0f;
                    colonyRW.ValueRW.Status = Space4XColonyStatus.Dormant;
                    continue;
                }

                var hasStock = _stockLookup.HasComponent(entity);
                ColonyIndustryStock stock = default;
                var suppliesReserve = colony.StoredResources;

                if (hasStock)
                {
                    stock = _stockLookup[entity];
                    suppliesReserve = math.max(0f, stock.SuppliesReserve);
                }

                var demandPerSecond = population * Space4XColonySupply.DemandPerPopulation * DemandToConsumptionScalar;
                var foodRequired = math.max(0f, demandPerSecond * deltaTime);
                var foodConsumed = math.min(suppliesReserve, foodRequired);
                suppliesReserve = math.max(0f, suppliesReserve - foodConsumed);

                if (hasStock)
                {
                    stock.SuppliesReserve = suppliesReserve;
                    stock.LastUpdateTick = tickTime.Tick;
                    _stockLookup[entity] = stock;
                }

                var storedResources = hasStock
                    ? math.max(0f, stock.OreReserve + stock.SuppliesReserve + stock.ResearchReserve)
                    : math.max(0f, colony.StoredResources - foodConsumed);

                var demand = Space4XColonySupply.ComputeDemand(population);
                var supplyRatio = Space4XColonySupply.ComputeSupplyRatio(suppliesReserve, demand);

                var populationDelta = 0f;
                if (foodRequired > 1e-4f)
                {
                    if (foodConsumed >= foodRequired * 0.999f && supplyRatio > 1f)
                    {
                        var surplus = math.max(0f, supplyRatio - 1f);
                        populationDelta = population * GrowthRatePerSecond * surplus * deltaTime;
                    }
                    else if (foodConsumed < foodRequired)
                    {
                        var deficit = math.saturate(1f - (foodConsumed / math.max(foodRequired, 1e-4f)));
                        populationDelta = -population * DeclineRatePerSecond * deficit * deltaTime;
                    }
                }

                population = math.max(0f, population + populationDelta);

                colonyRW.ValueRW.Population = population;
                colonyRW.ValueRW.StoredResources = storedResources;

                if (population <= 0f)
                {
                    colonyRW.ValueRW.Status = Space4XColonyStatus.Dormant;
                }
                else if (populationDelta > 0f)
                {
                    colonyRW.ValueRW.Status = Space4XColonyStatus.Growing;
                }
                else if (populationDelta < 0f)
                {
                    colonyRW.ValueRW.Status = Space4XColonyStatus.InCrisis;
                }
            }
        }
    }
}
