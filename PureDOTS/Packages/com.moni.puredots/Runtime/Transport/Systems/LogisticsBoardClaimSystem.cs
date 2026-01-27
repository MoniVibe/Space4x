using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Transport;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Transport.Systems
{
    /// <summary>
    /// Allocates reservations from logistics boards based on claim requests.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct LogisticsBoardClaimSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LogisticsBoard>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            foreach (var (board, config, demands, reservations, claims, entity) in
                     SystemAPI.Query<RefRW<LogisticsBoard>, RefRO<LogisticsBoardConfig>, DynamicBuffer<LogisticsDemandEntry>,
                             DynamicBuffer<LogisticsReservationEntry>, DynamicBuffer<LogisticsClaimRequest>>()
                         .WithEntityAccess())
            {
                var demandBuffer = demands;
                var reservationBuffer = reservations;
                var claimBuffer = claims;

                ExpireReservations(timeState.Tick, reservationBuffer);

                if (claimBuffer.Length == 0 || demandBuffer.Length == 0)
                {
                    claimBuffer.Clear();
                    continue;
                }

                var configValue = config.ValueRO;
                var maxClaims = configValue.MaxClaimsPerTick > 0 ? configValue.MaxClaimsPerTick : (byte)4;
                var claimsProcessed = 0;

                for (int i = 0; i < claimBuffer.Length; i++)
                {
                    if (claimsProcessed >= maxClaims)
                    {
                        break;
                    }

                    var claim = claimBuffer[i];
                    if (claim.Requester == Entity.Null || !state.EntityManager.Exists(claim.Requester))
                    {
                        continue;
                    }

                    if (HasActiveReservation(reservationBuffer, claim.Requester))
                    {
                        continue;
                    }

                    var demandIndex = SelectDemandIndex(demandBuffer, claim);
                    if (demandIndex < 0)
                    {
                        continue;
                    }

                    var demand = demandBuffer[demandIndex];
                    var allocation = ComputeAllocation(demand.OutstandingUnits, claim, configValue);
                    if (allocation <= 0f)
                    {
                        continue;
                    }

                    var reservationId = BuildReservationId(entity, claim.Requester, timeState.Tick, (uint)i);
                    var expiry = configValue.ReservationExpiryTicks > 0
                        ? timeState.Tick + configValue.ReservationExpiryTicks
                        : timeState.Tick + 60u;

                    reservationBuffer.Add(new LogisticsReservationEntry
                    {
                        ReservationId = reservationId,
                        HaulerEntity = claim.Requester,
                        SiteEntity = demand.SiteEntity,
                        SourceEntity = Entity.Null,
                        ResourceTypeIndex = demand.ResourceTypeIndex,
                        Units = allocation,
                        CreatedTick = timeState.Tick,
                        ExpiryTick = expiry,
                        Status = LogisticsReservationStatus.Active
                    });

                    demand.ReservedUnits += allocation;
                    demand.OutstandingUnits = math.max(0f, demand.RequiredUnits - demand.DeliveredUnits - demand.ReservedUnits);
                    demand.LastUpdateTick = timeState.Tick;
                    demandBuffer[demandIndex] = demand;

                    claimsProcessed++;
                }

                claimBuffer.Clear();
                board.ValueRW.LastUpdateTick = timeState.Tick;
            }
        }

        private static void ExpireReservations(uint tick, DynamicBuffer<LogisticsReservationEntry> reservations)
        {
            for (int i = 0; i < reservations.Length; i++)
            {
                var entry = reservations[i];
                if (entry.Status != LogisticsReservationStatus.Active)
                {
                    continue;
                }

                if (entry.ExpiryTick > 0 && tick >= entry.ExpiryTick)
                {
                    entry.Status = LogisticsReservationStatus.Expired;
                    reservations[i] = entry;
                }
            }
        }

        private static bool HasActiveReservation(DynamicBuffer<LogisticsReservationEntry> reservations, Entity requester)
        {
            for (int i = 0; i < reservations.Length; i++)
            {
                var entry = reservations[i];
                if (entry.Status == LogisticsReservationStatus.Active && entry.HaulerEntity == requester)
                {
                    return true;
                }
            }

            return false;
        }

        private static int SelectDemandIndex(DynamicBuffer<LogisticsDemandEntry> demands, in LogisticsClaimRequest claim)
        {
            var bestIndex = -1;
            var bestPriority = (byte)0;
            var bestOutstanding = 0f;

            for (int i = 0; i < demands.Length; i++)
            {
                var demand = demands[i];
                if (demand.OutstandingUnits <= 0f)
                {
                    continue;
                }

                if (claim.SiteFilter != Entity.Null && demand.SiteEntity != claim.SiteFilter)
                {
                    continue;
                }

                if (claim.ResourceTypeIndex != ushort.MaxValue && demand.ResourceTypeIndex != claim.ResourceTypeIndex)
                {
                    continue;
                }

                if (bestIndex < 0 || demand.Priority > bestPriority ||
                    (demand.Priority == bestPriority && demand.OutstandingUnits > bestOutstanding))
                {
                    bestIndex = i;
                    bestPriority = demand.Priority;
                    bestOutstanding = demand.OutstandingUnits;
                }
            }

            return bestIndex;
        }

        private static float ComputeAllocation(float outstanding, in LogisticsClaimRequest claim, in LogisticsBoardConfig config)
        {
            if (outstanding <= 0f)
            {
                return 0f;
            }

            var carryCap = claim.CarryCapacity > 0f ? claim.CarryCapacity : outstanding;
            var desiredMin = math.max(0f, claim.DesiredMinUnits);
            var desiredMax = claim.DesiredMaxUnits > 0f ? math.max(desiredMin, claim.DesiredMaxUnits) : carryCap;
            var maxBatch = config.MaxBatchUnits > 0f ? config.MaxBatchUnits : desiredMax;
            var minBatch = config.MinBatchUnits > 0f ? config.MinBatchUnits : 0f;

            var allocation = math.min(outstanding, math.min(carryCap, math.min(desiredMax, maxBatch)));
            var minNeeded = math.max(desiredMin, minBatch);
            if (allocation < minNeeded)
            {
                return 0f;
            }

            return allocation;
        }

        private static uint BuildReservationId(Entity boardEntity, Entity requester, uint tick, uint index)
        {
            var seed = math.hash(new uint4((uint)boardEntity.Index + 1u, (uint)requester.Index + 1u, tick, index + 11u));
            return seed == 0 ? 1u : seed;
        }
    }
}
