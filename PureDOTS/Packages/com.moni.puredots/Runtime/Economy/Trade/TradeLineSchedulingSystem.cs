using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Trade
{
    /// <summary>
    /// Checks trade line frequency and spawns transports when scheduled.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TradeLineSchedulingSystem : ISystem
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

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var tick = tickTimeState.Tick;

            foreach (var (tradeLine, entity) in SystemAPI.Query<RefRW<TradeLine>>().WithEntityAccess())
            {
                if (tick >= tradeLine.ValueRO.NextTripTick)
                {
                    // Spawn transport
                    // TODO: Create transport entity with route assignment
                    tradeLine.ValueRW.LastTripTick = tick;
                    tradeLine.ValueRW.NextTripTick = tick + (uint)(30f / tradeLine.ValueRO.Frequency); // Simple monthly frequency
                }
            }
        }
    }
}

