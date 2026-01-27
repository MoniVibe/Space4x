using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Wealth
{
    /// <summary>
    /// Handles wealth transfer on entity death via inheritance rules.
    /// Reads inheritance rules from data catalog and distributes wealth to heirs.
    /// </summary>
    // [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InheritanceSystem : ISystem
    {
        private ComponentLookup<VillagerWealth> _villagerWealthLookup;
        private ComponentLookup<FamilyWealth> _familyWealthLookup;
        private BufferLookup<WealthTransaction> _transactionBufferLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _villagerWealthLookup = state.GetComponentLookup<VillagerWealth>(false);
            _familyWealthLookup = state.GetComponentLookup<FamilyWealth>(false);
            _transactionBufferLookup = state.GetBufferLookup<WealthTransaction>(false);
        }

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

            _villagerWealthLookup.Update(ref state);
            _familyWealthLookup.Update(ref state);
            _transactionBufferLookup.Update(ref state);

            // Process entities marked for inheritance
            foreach (var (inheritance, entity) in SystemAPI.Query<RefRO<InheritancePending>>().WithEntityAccess())
            {
                ProcessInheritance(ref state, entity, inheritance.ValueRO);
            }
        }

        private void ProcessInheritance(ref SystemState state, Entity deceased, InheritancePending inheritance)
        {
            if (!_villagerWealthLookup.HasComponent(deceased))
            {
                return;
            }

            var deceasedWealth = _villagerWealthLookup[deceased];
            var totalWealth = deceasedWealth.Balance;

            if (totalWealth <= 0f)
            {
                // No wealth to inherit
                state.EntityManager.RemoveComponent<InheritancePending>(deceased);
                return;
            }

            // Simple primogeniture: primary heir gets all
            // TODO: Support more complex inheritance rules from catalog
            if (inheritance.PrimaryHeir != Entity.Null)
            {
                var heir = inheritance.PrimaryHeir;
                var inheritanceAmount = totalWealth;

                // Record transaction
                WealthTransactionSystem.RecordTransaction(
                    ref state,
                    deceased,
                    heir,
                    inheritanceAmount,
                    TransactionType.Exceptional,
                    new FixedString64Bytes("inheritance"),
                    new FixedString128Bytes("primogeniture")
                );

                // Drain deceased wallet
                deceasedWealth.Balance = 0f;
                _villagerWealthLookup[deceased] = deceasedWealth;
            }

            // Remove inheritance pending component
            state.EntityManager.RemoveComponent<InheritancePending>(deceased);
        }
    }

    /// <summary>
    /// Component marking an entity for inheritance processing.
    /// Added when entity dies, removed after inheritance is processed.
    /// </summary>
    public struct InheritancePending : IComponentData
    {
        public Entity PrimaryHeir;
    }

    /// <summary>
    /// Buffer of additional heirs for inheritance distribution.
    /// </summary>
    public struct InheritanceHeir : IBufferElementData
    {
        public Entity Heir;
        public float Share; // 0.0-1.0, fraction of inheritance
    }
}

