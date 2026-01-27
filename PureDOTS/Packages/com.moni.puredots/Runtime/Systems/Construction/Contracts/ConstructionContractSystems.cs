using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Construction.Contracts;
using PureDOTS.Runtime.Contracts;
using PureDOTS.Runtime.Logistics.Contracts;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Construction.Contracts
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ContractConstructionStateSystem : ISystem
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
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var ledgerEntity = SystemAPI.GetSingletonEntity<ContractReservationLedgerState>();
            var ledger = SystemAPI.GetBuffer<ContractReservationLedgerEntry>(ledgerEntity);

            foreach (var (site, requirements, entity) in SystemAPI.Query<
                RefRW<ContractConstructionSite>,
                DynamicBuffer<ContractConstructionRequirement>>().WithEntityAccess())
            {
                if (site.ValueRO.State == ContractConstructionState.Cancelled ||
                    site.ValueRO.State == ContractConstructionState.Complete)
                {
                    continue;
                }

                if (SystemAPI.HasComponent<ContractConstructionCancel>(entity))
                {
                    ReleaseReservations(ledger, entity);
                    site.ValueRW.State = ContractConstructionState.Cancelled;
                    site.ValueRW.StateTick = tick;
                    continue;
                }

                var hasAllReservations = RequirementsReserved(ledger, entity, requirements);
                switch (site.ValueRO.State)
                {
                    case ContractConstructionState.Planned:
                        if (hasAllReservations)
                        {
                            site.ValueRW.State = ContractConstructionState.Reserved;
                            site.ValueRW.StateTick = tick;
                        }
                        break;
                    case ContractConstructionState.Reserved:
                        if (hasAllReservations)
                        {
                            site.ValueRW.State = ContractConstructionState.Building;
                            site.ValueRW.StateTick = tick;
                        }
                        break;
                    case ContractConstructionState.Building:
                        if (hasAllReservations && tick > site.ValueRO.StateTick)
                        {
                            site.ValueRW.State = ContractConstructionState.Complete;
                            site.ValueRW.StateTick = tick;
                        }
                        break;
                }
            }
        }

        private static bool RequirementsReserved(DynamicBuffer<ContractReservationLedgerEntry> ledger, Entity owner, DynamicBuffer<ContractConstructionRequirement> requirements)
        {
            for (int i = 0; i < requirements.Length; i++)
            {
                var requirement = requirements[i];
                var reserved = 0;
                for (int l = 0; l < ledger.Length; l++)
                {
                    var entry = ledger[l];
                    if (entry.Owner == owner && entry.ResourceId == requirement.ResourceId && entry.State == ReservationState.Held)
                    {
                        reserved += entry.Amount;
                    }
                }

                if (reserved < requirement.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ReleaseReservations(DynamicBuffer<ContractReservationLedgerEntry> ledger, Entity owner)
        {
            for (int i = 0; i < ledger.Length; i++)
            {
                var entry = ledger[i];
                if (entry.Owner != owner || entry.State != ReservationState.Held)
                {
                    continue;
                }

                entry.State = ReservationState.Released;
                ledger[i] = entry;
            }
        }
    }
}
