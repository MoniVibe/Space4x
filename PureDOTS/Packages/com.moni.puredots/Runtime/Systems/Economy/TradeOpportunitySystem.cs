using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Economy
{
    /// <summary>
    /// Surfaces trade opportunities between batch inventories by comparing fill levels and price multipliers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BatchPricingSystem))]
    public partial struct TradeOpportunitySystem : ISystem
    {
        private struct SupplyCandidate
        {
            public Entity Entity;
            public float Units;
            public float Price;
            public float Fill;
        }

        private struct DemandCandidate
        {
            public Entity Entity;
            public float Price;
            public float Fill;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BatchInventory>();
            state.RequireForUpdate<BatchPricingState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TradeOpportunityState>();
            EnsureSingleton(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var settings = SystemAPI.TryGetSingleton<TradeOpportunitySettings>(out var cfg)
                ? cfg
                : TradeOpportunitySettings.CreateDefault();

            var supplyMap = new NativeParallelHashMap<FixedString64Bytes, SupplyCandidate>(16, Allocator.Temp);
            var demandMap = new NativeParallelHashMap<FixedString64Bytes, DemandCandidate>(16, Allocator.Temp);

            foreach (var (inventory, pricing, entity) in SystemAPI.Query<RefRO<BatchInventory>, RefRO<BatchPricingState>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasBuffer<InventoryBatch>(entity))
                {
                    continue;
                }

                var batches = state.EntityManager.GetBuffer<InventoryBatch>(entity);
                if (batches.Length == 0)
                {
                    continue;
                }

                var resourceTotals = new NativeParallelHashMap<FixedString64Bytes, float>(batches.Length, Allocator.Temp);
                for (int i = 0; i < batches.Length; i++)
                {
                    var batch = batches[i];
                    if (batch.Units <= 0f)
                    {
                        continue;
                    }

                    if (resourceTotals.TryGetValue(batch.ResourceId, out var existing))
                    {
                        resourceTotals[batch.ResourceId] = existing + batch.Units;
                    }
                    else
                    {
                        resourceTotals.Add(batch.ResourceId, batch.Units);
                    }
                }

                var fill = inventory.ValueRO.MaxCapacity > 0f
                    ? math.saturate(inventory.ValueRO.TotalUnits / inventory.ValueRO.MaxCapacity)
                    : 0f;

                var totals = resourceTotals.GetKeyValueArrays(Allocator.Temp);
                for (int i = 0; i < totals.Length; i++)
                {
                    var key = totals.Keys[i];
                    var units = totals.Values[i];
                    if (units < settings.MinTradeUnits)
                    {
                        continue;
                    }

                    if (fill >= settings.SupplyFillThreshold)
                    {
                        var candidate = new SupplyCandidate
                        {
                            Entity = entity,
                            Units = units,
                            Price = pricing.ValueRO.LastPriceMultiplier,
                            Fill = fill
                        };

                        if (!supplyMap.TryGetValue(key, out var existing) || candidate.Price < existing.Price)
                        {
                            supplyMap[key] = candidate;
                        }
                    }

                    if (fill <= settings.DemandFillThreshold)
                    {
                        var demand = new DemandCandidate
                        {
                            Entity = entity,
                            Price = pricing.ValueRO.LastPriceMultiplier,
                            Fill = fill
                        };

                        if (!demandMap.TryGetValue(key, out var existingDemand) || demand.Price > existingDemand.Price)
                        {
                            demandMap[key] = demand;
                        }
                    }
                }

                resourceTotals.Dispose();
            }

            if (supplyMap.IsEmpty || demandMap.IsEmpty)
            {
                return;
            }

            var opportunities = new NativeList<TradeOpportunity>(Allocator.Temp);
            var supplyPairs = supplyMap.GetKeyValueArrays(Allocator.Temp);
            for (int i = 0; i < supplyPairs.Length; i++)
            {
                var resourceId = supplyPairs.Keys[i];
                var supply = supplyPairs.Values[i];
                if (!demandMap.TryGetValue(resourceId, out var demand))
                {
                    continue;
                }

                if (supply.Entity == demand.Entity)
                {
                    continue;
                }

                var spread = demand.Price - supply.Price;
                if (spread < settings.MinSpread)
                {
                    continue;
                }

                opportunities.Add(new TradeOpportunity
                {
                    ResourceId = resourceId,
                    Source = supply.Entity,
                    Destination = demand.Entity,
                    PriceSpread = spread,
                    AvailableUnits = supply.Units,
                    Tick = timeState.Tick
                });
            }

            if (opportunities.Length == 0)
            {
                return;
            }

            NativeSortExtension.Sort(opportunities.AsArray());
            var singleton = SystemAPI.GetSingletonEntity<TradeOpportunityState>();
            var buffer = state.EntityManager.GetBuffer<TradeOpportunity>(singleton);

            var max = math.min(settings.MaxOpportunities, opportunities.Length);
            var changed = buffer.Length != max;
            var compareLength = math.min(buffer.Length, max);
            for (int i = 0; i < compareLength && !changed; i++)
            {
                var current = buffer[i];
                var candidate = opportunities[i];
                if (!current.ResourceId.Equals(candidate.ResourceId)
                    || current.Source != candidate.Source
                    || current.Destination != candidate.Destination
                    || math.abs(current.PriceSpread - candidate.PriceSpread) > 0.0001f
                    || math.abs(current.AvailableUnits - candidate.AvailableUnits) > 0.0001f)
                {
                    changed = true;
                }
            }

            buffer.Clear();
            buffer.ResizeUninitialized(max);
            for (int i = 0; i < max; i++)
            {
                buffer[i] = opportunities[i];
            }

            var stateData = state.EntityManager.GetComponentData<TradeOpportunityState>(singleton);
            if (changed)
            {
                stateData.Version++;
            }
            stateData.OpportunityCount = max;
            stateData.LastUpdateTick = timeState.Tick;
            state.EntityManager.SetComponentData(singleton, stateData);

            opportunities.Dispose();
            supplyMap.Dispose();
            demandMap.Dispose();
        }

        private static void EnsureSingleton(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TradeOpportunityState>();
            using var query = queryBuilder.Build(entityManager);

            Entity singleton;
            if (query.IsEmptyIgnoreFilter)
            {
                singleton = entityManager.CreateEntity();
                entityManager.AddComponentData(singleton, new TradeOpportunityState
                {
                    Version = 0,
                    LastUpdateTick = 0,
                    OpportunityCount = 0
                });
                entityManager.AddBuffer<TradeOpportunity>(singleton);
            }
            else
            {
                singleton = query.GetSingletonEntity();
                if (!entityManager.HasBuffer<TradeOpportunity>(singleton))
                {
                    entityManager.AddBuffer<TradeOpportunity>(singleton);
                }
            }

            if (!entityManager.HasComponent<TradeOpportunitySettings>(singleton))
            {
                entityManager.AddComponentData(singleton, TradeOpportunitySettings.CreateDefault());
            }

            if (!entityManager.HasComponent<TradeRoutingState>(singleton))
            {
                entityManager.AddComponentData(singleton, new TradeRoutingState { LastProcessedVersion = 0 });
            }
        }
    }
}
