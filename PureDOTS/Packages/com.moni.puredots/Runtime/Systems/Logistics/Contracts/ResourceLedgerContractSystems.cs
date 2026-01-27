using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Contracts;
using PureDOTS.Runtime.Logistics.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Logistics.Contracts
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ContractReservationRequestSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ContractHarnessEnabled>();
            state.RequireForUpdate<ContractReservationLedgerState>();
            state.RequireForUpdate<ContractInvariantCounters>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var ledgerEntity = SystemAPI.GetSingletonEntity<ContractReservationLedgerState>();
            var ledgerBuffer = SystemAPI.GetBuffer<ContractReservationLedgerEntry>(ledgerEntity);
            var ledgerState = SystemAPI.GetSingleton<ContractReservationLedgerState>();

            foreach (var (requests, inventory, entity) in SystemAPI.Query<
                DynamicBuffer<ContractReservationRequest>,
                DynamicBuffer<ContractInventory>>().WithEntityAccess())
            {
                if (requests.Length == 0)
                {
                    continue;
                }

                for (int i = requests.Length - 1; i >= 0; i--)
                {
                    var request = requests[i];
                    if (request.Amount <= 0)
                    {
                        requests.RemoveAt(i);
                        continue;
                    }

                    var available = GetAvailable(inventory, ledgerBuffer, entity, request.ResourceId);
                    if (available < request.Amount)
                    {
                        continue;
                    }

                    ledgerBuffer.Add(new ContractReservationLedgerEntry
                    {
                        ReservationId = ledgerState.NextReservationId++,
                        ResourceId = request.ResourceId,
                        Amount = request.Amount,
                        Owner = entity,
                        State = ReservationState.Held,
                        ExpireTick = request.ExpireTick == 0 ? time.Tick + 2 : request.ExpireTick,
                        CommittedTick = 0,
                        LastStateTick = time.Tick
                    });

                    requests.RemoveAt(i);
                }
            }

            SystemAPI.SetSingleton(ledgerState);
        }

        private static int GetAvailable(DynamicBuffer<ContractInventory> inventory, DynamicBuffer<ContractReservationLedgerEntry> ledger, Entity owner, int resourceId)
        {
            var total = GetInventoryAmount(inventory, resourceId);
            var reserved = 0;
            for (int i = 0; i < ledger.Length; i++)
            {
                var entry = ledger[i];
                if (entry.Owner != owner || entry.ResourceId != resourceId)
                {
                    continue;
                }

                if (entry.State == ReservationState.Held)
                {
                    reserved += entry.Amount;
                }
            }

            return total - reserved;
        }

        private static int GetInventoryAmount(DynamicBuffer<ContractInventory> inventory, int resourceId)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ResourceId == resourceId)
                {
                    return inventory[i].Amount;
                }
            }

            return 0;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ContractReservationRequestSystem))]
    public partial struct ContractReservationLedgerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ContractHarnessEnabled>();
            state.RequireForUpdate<ContractReservationLedgerState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var ledgerEntity = SystemAPI.GetSingletonEntity<ContractReservationLedgerState>();
            var ledgerBuffer = SystemAPI.GetBuffer<ContractReservationLedgerEntry>(ledgerEntity);
            var counters = SystemAPI.GetSingletonRW<ContractInvariantCounters>();
            var hasStream = SystemAPI.TryGetSingletonEntity<ContractViolationStream>(out var streamEntity);
            DynamicBuffer<ContractViolationEvent> ringBuffer = default;
            ContractViolationRingState ringState = default;
            if (hasStream)
            {
                ringBuffer = SystemAPI.GetBuffer<ContractViolationEvent>(streamEntity);
                ringState = SystemAPI.GetComponent<ContractViolationRingState>(streamEntity);
            }
            for (int i = ledgerBuffer.Length - 1; i >= 0; i--)
            {
                var entry = ledgerBuffer[i];
                if (entry.State == ReservationState.Held && entry.ExpireTick > 0 && time.Tick >= entry.ExpireTick)
                {
                    entry.State = ReservationState.Released;
                    entry.LastStateTick = time.Tick;
                    ledgerBuffer[i] = entry;
                    counters.ValueRW.ExpiredReservationCount += 1;
                    if (hasStream)
                    {
                        ContractViolationLogger.LogViolation(ref ringBuffer, ref ringState, new ContractViolationEvent
                        {
                            ContractId = ContractLedgerConstants.ResourceLedgerContractId,
                            Tick = time.Tick,
                            Subject = entry.Owner,
                            ReservationId = entry.ReservationId,
                            Reason = (byte)ContractViolationReason.ExpiredReservation
                        });
                    }
                }
            }

            if (hasStream)
            {
                SystemAPI.SetComponent(streamEntity, ringState);
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ContractReservationLedgerSystem))]
    public partial struct ContractLedgerInvariantSystem : ISystem
    {
        private EntityQuery _inventoryQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ContractHarnessEnabled>();
            state.RequireForUpdate<ContractReservationLedgerState>();
            state.RequireForUpdate<ContractInvariantCounters>();
            state.RequireForUpdate<TimeState>();
            _inventoryQuery = state.GetEntityQuery(typeof(ContractInventory));
        }

        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var ledgerEntity = SystemAPI.GetSingletonEntity<ContractReservationLedgerState>();
            var ledgerBuffer = SystemAPI.GetBuffer<ContractReservationLedgerEntry>(ledgerEntity);
            var counters = SystemAPI.GetSingleton<ContractInvariantCounters>();
            var hasStream = SystemAPI.TryGetSingletonEntity<ContractViolationStream>(out var streamEntity);
            DynamicBuffer<ContractViolationEvent> ringBuffer = default;
            ContractViolationRingState ringState = default;
            if (hasStream)
            {
                ringBuffer = SystemAPI.GetBuffer<ContractViolationEvent>(streamEntity);
                ringState = SystemAPI.GetComponent<ContractViolationRingState>(streamEntity);
            }

            using var reservationIds = new NativeHashSet<uint>(ledgerBuffer.Length, Allocator.Temp);
            for (int i = 0; i < ledgerBuffer.Length; i++)
            {
                var entry = ledgerBuffer[i];
                if (!reservationIds.Add(entry.ReservationId))
                {
                    counters.DuplicateReservationIdCount += 1;
                    if (hasStream)
                    {
                        ContractViolationLogger.LogViolation(ref ringBuffer, ref ringState, new ContractViolationEvent
                        {
                            ContractId = ContractLedgerConstants.ResourceLedgerContractId,
                            Tick = tick,
                            Subject = entry.Owner,
                            ReservationId = entry.ReservationId,
                            Reason = (byte)ContractViolationReason.DuplicateReservationId
                        });
                    }
                }

                if (entry.State == ReservationState.Held && entry.CommittedTick != 0)
                {
                    counters.IllegalStateTransitionCount += 1;
                    if (hasStream)
                    {
                        ContractViolationLogger.LogViolation(ref ringBuffer, ref ringState, new ContractViolationEvent
                        {
                            ContractId = ContractLedgerConstants.ResourceLedgerContractId,
                            Tick = tick,
                            Subject = entry.Owner,
                            ReservationId = entry.ReservationId,
                            Reason = (byte)ContractViolationReason.IllegalLedgerTransition
                        });
                    }
                }

                if (entry.State == ReservationState.Released && entry.LastStateTick < entry.CommittedTick)
                {
                    counters.IllegalStateTransitionCount += 1;
                    if (hasStream)
                    {
                        ContractViolationLogger.LogViolation(ref ringBuffer, ref ringState, new ContractViolationEvent
                        {
                            ContractId = ContractLedgerConstants.ResourceLedgerContractId,
                            Tick = tick,
                            Subject = entry.Owner,
                            ReservationId = entry.ReservationId,
                            Reason = (byte)ContractViolationReason.IllegalLedgerTransition
                        });
                    }
                }
            }

            using var entities = _inventoryQuery.ToEntityArray(Allocator.Temp);
            for (int e = 0; e < entities.Length; e++)
            {
                var entity = entities[e];
                var inventory = SystemAPI.GetBuffer<ContractInventory>(entity);
                for (int i = 0; i < inventory.Length; i++)
                {
                    if (inventory[i].Amount < 0)
                    {
                        counters.NegativeInventoryCount += 1;
                        if (hasStream)
                        {
                            ContractViolationLogger.LogViolation(ref ringBuffer, ref ringState, new ContractViolationEvent
                            {
                                ContractId = ContractLedgerConstants.ResourceLedgerContractId,
                                Tick = tick,
                                Subject = entity,
                                ReservationId = 0,
                                Reason = (byte)ContractViolationReason.NegativeInventory
                            });
                        }
                    }

                    var reserved = 0;
                    for (int l = 0; l < ledgerBuffer.Length; l++)
                    {
                        var entry = ledgerBuffer[l];
                        if (entry.Owner != entity || entry.ResourceId != inventory[i].ResourceId)
                        {
                            continue;
                        }

                        if (entry.State == ReservationState.Held)
                        {
                            reserved += entry.Amount;
                        }
                    }

                    if (reserved > inventory[i].Amount)
                    {
                        counters.ReservedOverAvailableCount += 1;
                        if (hasStream)
                        {
                            ContractViolationLogger.LogViolation(ref ringBuffer, ref ringState, new ContractViolationEvent
                            {
                                ContractId = ContractLedgerConstants.ResourceLedgerContractId,
                                Tick = tick,
                                Subject = entity,
                                ReservationId = 0,
                                Reason = (byte)ContractViolationReason.ReservedExceedsAvailable
                            });
                        }
                    }
                }
            }

            SystemAPI.SetSingleton(counters);
            if (hasStream)
            {
                SystemAPI.SetComponent(streamEntity, ringState);
            }
        }

    }

    internal static class ContractLedgerConstants
    {
        internal static readonly FixedString64Bytes ResourceLedgerContractId = BuildResourceLedgerContractId();

        private static FixedString64Bytes BuildResourceLedgerContractId()
        {
            var id = new FixedString64Bytes();
            id.Append('C');
            id.Append('O');
            id.Append('N');
            id.Append('T');
            id.Append('R');
            id.Append('A');
            id.Append('C');
            id.Append('T');
            id.Append(':');
            id.Append('R');
            id.Append('E');
            id.Append('S');
            id.Append('O');
            id.Append('U');
            id.Append('R');
            id.Append('C');
            id.Append('E');
            id.Append('.');
            id.Append('L');
            id.Append('E');
            id.Append('D');
            id.Append('G');
            id.Append('E');
            id.Append('R');
            id.Append('.');
            id.Append('V');
            id.Append('1');
            return id;
        }
    }

    internal static class ContractViolationLogger
    {
        internal static void LogViolation(ref DynamicBuffer<ContractViolationEvent> buffer, ref ContractViolationRingState ringState, in ContractViolationEvent entry)
        {
            if (ringState.Capacity <= 0 || buffer.Length == 0)
            {
                return;
            }

            var index = ringState.WriteIndex % ringState.Capacity;
            buffer[index] = entry;
            ringState.WriteIndex = (index + 1) % ringState.Capacity;
        }
    }
}
