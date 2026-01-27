using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Signals;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SignalBusSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SignalBus>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var busEntity = SystemAPI.GetSingletonEntity<SignalBus>();
            var bus = SystemAPI.GetComponentRW<SignalBus>(busEntity);
            var signals = state.EntityManager.GetBuffer<SignalEvent>(busEntity);

            var maxSignals = SignalBusConfig.CreateDefault().MaxSignals;
            if (SystemAPI.TryGetSingleton(out SignalBusConfig config))
            {
                maxSignals = math.max(0, config.MaxSignals);
            }

            var previousLength = signals.Length;
            if (maxSignals > 0 && signals.Length > maxSignals)
            {
                var dropCount = signals.Length - maxSignals;
                signals.RemoveRange(0, dropCount);
                bus.ValueRW.DroppedCount += dropCount;
            }

            bus.ValueRW.PendingCount = signals.Length;
            var currentTick = SystemAPI.HasSingleton<TimeState>()
                ? SystemAPI.GetSingleton<TimeState>().Tick
                : bus.ValueRO.LastWriteTick;
            bus.ValueRW.LastWriteTick = currentTick;

            if (signals.Length != previousLength)
            {
                bus.ValueRW.Version++;
            }
        }
    }
}
