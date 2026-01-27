using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Policies
{
    /// <summary>
    /// Applies tariffs on trade routes (Chunk 4) and market trades (Chunk 5).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TariffApplicationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Process tariff requests
            foreach (var (tariffRequest, entity) in SystemAPI.Query<RefRO<TariffApplicationRequest>>().WithEntityAccess())
            {
                // Apply tariff to trade
                // Simplified - should add cost to trade routes/market trades
                state.EntityManager.RemoveComponent<TariffApplicationRequest>(entity);
            }
        }
    }

    /// <summary>
    /// Request to apply tariff.
    /// </summary>
    public struct TariffApplicationRequest : IComponentData
    {
        public Entity TariffPolicyEntity;
        public Entity TradeEntity;
        public float TradeValue;
    }
}

