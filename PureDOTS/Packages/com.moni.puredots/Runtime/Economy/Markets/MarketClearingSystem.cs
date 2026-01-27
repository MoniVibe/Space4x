using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime.Economy.Wealth;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Markets
{
    /// <summary>
    /// Matches buy/sell intents at CurrentPrice, executes trades.
    /// Connects to Chunk 1 (transactions) and Chunk 2 (inventory moves).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MarketClearingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
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

            // Process buy/sell intents
            foreach (var (buyIntent, entity) in SystemAPI.Query<RefRO<MarketBuyIntent>>().WithEntityAccess())
            {
                // Match with sell intents and execute trade
                // Simplified - should match at CurrentPrice
                state.EntityManager.RemoveComponent<MarketBuyIntent>(entity);
            }

            foreach (var (sellIntent, entity) in SystemAPI.Query<RefRO<MarketSellIntent>>().WithEntityAccess())
            {
                // Match with buy intents and execute trade
                // Simplified - should match at CurrentPrice
                state.EntityManager.RemoveComponent<MarketSellIntent>(entity);
            }
        }
    }
}

