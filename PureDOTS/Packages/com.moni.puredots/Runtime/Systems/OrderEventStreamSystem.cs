using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Orders;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct OrderEventStreamSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OrderEventStream>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var streamEntity = SystemAPI.GetSingletonEntity<OrderEventStream>();
            var stream = SystemAPI.GetComponentRW<OrderEventStream>(streamEntity);
            var events = state.EntityManager.GetBuffer<OrderEvent>(streamEntity);

            var maxEvents = OrderEventStreamConfig.CreateDefault().MaxEvents;
            if (SystemAPI.TryGetSingleton(out OrderEventStreamConfig config))
            {
                maxEvents = math.max(0, config.MaxEvents);
            }

            var previousLength = events.Length;
            if (maxEvents > 0 && events.Length > maxEvents)
            {
                var dropCount = events.Length - maxEvents;
                events.RemoveRange(0, dropCount);
                stream.ValueRW.DroppedEvents += dropCount;
            }

            stream.ValueRW.EventCount = events.Length;

            var currentTick = SystemAPI.HasSingleton<TimeState>()
                ? SystemAPI.GetSingleton<TimeState>().Tick
                : stream.ValueRO.LastWriteTick;
            stream.ValueRW.LastWriteTick = currentTick;

            if (events.Length != previousLength)
            {
                stream.ValueRW.Version++;
            }
        }
    }
}
