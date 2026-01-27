using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct HaulTicketExpirySystem : ISystem
    {
        private const uint DefaultExpiryTicks = 600;

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

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return;
            }

            var expiryTicks = DefaultExpiryTicks;
            var requeueOnExpiry = true;
            if (SystemAPI.TryGetSingleton<HaulTicketPolicy>(out var policy))
            {
                expiryTicks = policy.DefaultExpiryTicks > 0 ? policy.DefaultExpiryTicks : DefaultExpiryTicks;
                requeueOnExpiry = policy.RequeueOnExpiry != 0;
            }

            var tick = tickTime.Tick;
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (ticket, entity) in SystemAPI.Query<RefRW<HaulTicket>>().WithEntityAccess())
            {
                if (ticket.ValueRO.Status == HaulTicketStatus.Delivered ||
                    ticket.ValueRO.Status == HaulTicketStatus.Cancelled ||
                    ticket.ValueRO.Status == HaulTicketStatus.Expired)
                {
                    continue;
                }

                if (ticket.ValueRO.ExpiryTick == 0 || tick < ticket.ValueRO.ExpiryTick)
                {
                    continue;
                }

                ticket.ValueRW.Status = HaulTicketStatus.Expired;

                if (!requeueOnExpiry || ticket.ValueRO.RemainingAmount <= 0f)
                {
                    continue;
                }

                var newEntity = ecb.CreateEntity();
                ecb.AddComponent(newEntity, new HaulTicket
                {
                    SourceStorage = ticket.ValueRO.SourceStorage,
                    DestinationStorage = ticket.ValueRO.DestinationStorage,
                    ResourceId = ticket.ValueRO.ResourceId,
                    ResourceTypeIndex = ticket.ValueRO.ResourceTypeIndex,
                    RequestedAmount = ticket.ValueRO.RemainingAmount,
                    RemainingAmount = ticket.ValueRO.RemainingAmount,
                    Priority = ticket.ValueRO.Priority,
                    CreatedTick = tick,
                    ExpiryTick = tick + expiryTicks,
                    JobEntity = ticket.ValueRO.JobEntity,
                    Status = HaulTicketStatus.Pending
                });
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
