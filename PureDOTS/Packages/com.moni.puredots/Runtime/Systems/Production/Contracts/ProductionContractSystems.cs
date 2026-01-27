using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Contracts;
using PureDOTS.Runtime.Logistics.Contracts;
using PureDOTS.Runtime.Production.Contracts;
using PureDOTS.Systems.Logistics.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Production.Contracts
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Logistics.Contracts.ContractReservationLedgerSystem))]
    public partial struct ContractProductionReducerSystem : ISystem
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
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
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

            foreach (var (request, result, inventory, entity) in SystemAPI.Query<
                RefRW<ContractProductionRequest>,
                RefRW<ContractProductionResult>,
                DynamicBuffer<ContractInventory>>().WithEntityAccess())
            {
                if (request.ValueRO.LastProcessedTick == tick)
                {
                    continue;
                }

                request.ValueRW.LastProcessedTick = tick;
                result.ValueRW.LastProcessedTick = tick;
                result.ValueRW.Success = 0;
                result.ValueRW.ConsumedAmount = 0;
                result.ValueRW.ProducedAmount = 0;
                result.ValueRW.FailureReason = 1;

                if (!TryCommitReservation(ledgerBuffer, entity, request.ValueRO.InputResourceId, request.ValueRO.InputAmount, tick, ref counters, hasStream, ref ringBuffer, ref ringState))
                {
                    continue;
                }

                if (!TryConsumeInventory(inventory, request.ValueRO.InputResourceId, request.ValueRO.InputAmount))
                {
                    result.ValueRW.FailureReason = 2;
                    continue;
                }

                AddInventory(inventory, request.ValueRO.OutputResourceId, request.ValueRO.OutputAmount);

                result.ValueRW.Success = 1;
                result.ValueRW.FailureReason = 0;
                result.ValueRW.ConsumedAmount = request.ValueRO.InputAmount;
                result.ValueRW.ProducedAmount = request.ValueRO.OutputAmount;
            }

            if (hasStream)
            {
                SystemAPI.SetComponent(streamEntity, ringState);
            }
        }

        private static bool TryCommitReservation(
            DynamicBuffer<ContractReservationLedgerEntry> ledgerBuffer,
            Entity owner,
            int resourceId,
            int amount,
            uint tick,
            ref RefRW<ContractInvariantCounters> counters,
            bool hasStream,
            ref DynamicBuffer<ContractViolationEvent> ringBuffer,
            ref ContractViolationRingState ringState)
        {
            for (int i = 0; i < ledgerBuffer.Length; i++)
            {
                var entry = ledgerBuffer[i];
                if (entry.Owner != owner || entry.ResourceId != resourceId)
                {
                    continue;
                }

                if (entry.State == ReservationState.Committed)
                {
                    counters.ValueRW.DoubleCommitAttemptCount += 1;
                    if (hasStream)
                    {
                        LogViolation(ref ringBuffer, ref ringState, new ContractViolationEvent
                        {
                            ContractId = ContractLedgerConstants.ResourceLedgerContractId,
                            Tick = tick,
                            Subject = owner,
                            ReservationId = entry.ReservationId,
                            Reason = (byte)ContractViolationReason.DoubleCommitAttempt
                        });
                    }
                    return false;
                }

                if (entry.State == ReservationState.Released)
                {
                    counters.ValueRW.CommitWithoutHoldCount += 1;
                    if (hasStream)
                    {
                        LogViolation(ref ringBuffer, ref ringState, new ContractViolationEvent
                        {
                            ContractId = ContractLedgerConstants.ResourceLedgerContractId,
                            Tick = tick,
                            Subject = owner,
                            ReservationId = entry.ReservationId,
                            Reason = (byte)ContractViolationReason.CommitWithoutHold
                        });
                    }
                    return false;
                }

                if (entry.State == ReservationState.Held && entry.Amount >= amount)
                {
                    entry.State = ReservationState.Committed;
                    entry.CommittedTick = tick;
                    entry.LastStateTick = tick;
                    ledgerBuffer[i] = entry;
                    return true;
                }
            }

            return false;
        }

        private static void LogViolation(ref DynamicBuffer<ContractViolationEvent> buffer, ref ContractViolationRingState ringState, in ContractViolationEvent entry)
        {
            if (ringState.Capacity <= 0 || buffer.Length == 0)
            {
                return;
            }

            var index = ringState.WriteIndex % ringState.Capacity;
            buffer[index] = entry;
            ringState.WriteIndex = (index + 1) % ringState.Capacity;
        }

        private static bool TryConsumeInventory(DynamicBuffer<ContractInventory> inventory, int resourceId, int amount)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ResourceId != resourceId)
                {
                    continue;
                }

                if (inventory[i].Amount < amount)
                {
                    return false;
                }

                var entry = inventory[i];
                entry.Amount -= amount;
                inventory[i] = entry;
                return true;
            }

            return false;
        }

        private static void AddInventory(DynamicBuffer<ContractInventory> inventory, int resourceId, int amount)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ResourceId != resourceId)
                {
                    continue;
                }

                var entry = inventory[i];
                entry.Amount += amount;
                inventory[i] = entry;
                return;
            }

            inventory.Add(new ContractInventory
            {
                ResourceId = resourceId,
                Amount = amount
            });
        }
    }
}
