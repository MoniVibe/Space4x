using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy;
using PureDOTS.Runtime.Transport;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Economy
{
    /// <summary>
    /// Lightweight assignment/fulfillment stub for trade-generated logistics requests.
    /// Assigns a single virtual transport and incrementally fulfills the request to unblock downstream registry metrics.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TransportPhaseGroup))]
    // Removed invalid UpdateAfter: TradeRoutingSystem runs in ColdPathSystemGroup.
    [UpdateBefore(typeof(LogisticsRequestRegistrySystem))]
    public partial struct TradeTransportAssignmentSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TradeOpportunityState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var deliveryRate = math.max(0.1f, timeState.CurrentSpeedMultiplier); // simple deterministic rate

            foreach (var (request, progress, entity) in SystemAPI.Query<RefRW<LogisticsRequest>, RefRW<LogisticsRequestProgress>>().WithAll<TradeRouteRequestTag>().WithEntityAccess())
            {
                var remaining = math.max(0f, request.ValueRO.RequestedUnits - request.ValueRO.FulfilledUnits);
                if (remaining <= 0.0001f)
                {
                    // Mark complete by removing the tag so downstream systems can ignore it.
                    ecb.RemoveComponent<TradeRouteRequestTag>(entity);
                    continue;
                }

                if (progress.ValueRO.AssignedTransportCount == 0)
                {
                    progress.ValueRW.AssignedTransportCount = 1;
                    progress.ValueRW.AssignedUnits = request.ValueRO.RequestedUnits;
                    progress.ValueRW.LastAssignmentTick = timeState.Tick;
                }

                var delivered = math.min(remaining, deliveryRate);
                request.ValueRW.FulfilledUnits += delivered;
                request.ValueRW.LastUpdateTick = timeState.Tick;
                progress.ValueRW.LastAssignmentTick = timeState.Tick;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
