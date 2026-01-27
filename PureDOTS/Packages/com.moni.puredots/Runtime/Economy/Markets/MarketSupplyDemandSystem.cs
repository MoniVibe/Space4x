using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Markets
{
    /// <summary>
    /// Calculates supply from inventories (Chunk 2) and demand from consumption/projection.
    /// Updates MarketPrice.Supply and Demand fields.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MarketSupplyDemandSystem : ISystem
    {
        private ComponentLookup<Inventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemBufferLookup;
        private BufferLookup<MarketPrice> _marketPriceLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ItemSpecCatalog>();
            _inventoryLookup = state.GetComponentLookup<Inventory>(false);
            _itemBufferLookup = state.GetBufferLookup<InventoryItem>(false);
            _marketPriceLookup = state.GetBufferLookup<MarketPrice>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ItemSpecCatalog>(out var itemCatalog))
            {
                return;
            }

            _inventoryLookup.Update(ref state);
            _itemBufferLookup.Update(ref state);
            _marketPriceLookup.Update(ref state);

            // Calculate supply/demand for each settlement with market
            foreach (var (market, entity) in SystemAPI.Query<RefRO<Market>>().WithEntityAccess())
            {
                if (!_marketPriceLookup.HasBuffer(entity))
                {
                    continue;
                }

                var prices = _marketPriceLookup[entity];
                
                // Calculate supply from local inventories
                // Simplified - should aggregate all inventories in settlement
                for (int i = 0; i < prices.Length; i++)
                {
                    var price = prices[i];
                    // TODO: Aggregate supply from inventories
                    price.Supply = 0f; // Placeholder
                    price.Demand = 100f; // Placeholder - should calculate from consumption patterns
                    prices[i] = price;
                }
            }
        }
    }

    /// <summary>
    /// Tag component for settlement market.
    /// </summary>
    public struct Market : IComponentData
    {
    }
}

