using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Wealth;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Policies
{
    /// <summary>
    /// Calculates taxes, records transactions.
    /// Income tax, business tax, transaction tax using Chunk 1 transaction APIs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TaxCollectionSystem : ISystem
    {
        private FixedString64Bytes _incomeTaxReason;
        private FixedString64Bytes _businessTaxReason;
        private FixedString128Bytes _taxCollectionChannel;

        private ComponentLookup<TaxPolicy> _taxPolicyLookup;
        private ComponentLookup<VillagerWealth> _villagerWealthLookup;
        private ComponentLookup<BusinessBalance> _businessBalanceLookup;
        private ComponentLookup<VillageTreasury> _villageTreasuryLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _taxPolicyLookup = state.GetComponentLookup<TaxPolicy>(false);
            _villagerWealthLookup = state.GetComponentLookup<VillagerWealth>(false);
            _businessBalanceLookup = state.GetComponentLookup<BusinessBalance>(false);
            _villageTreasuryLookup = state.GetComponentLookup<VillageTreasury>(false);

            _incomeTaxReason = "income_tax";
            _businessTaxReason = "business_tax";
            _taxCollectionChannel = "tax_collection";
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

            _taxPolicyLookup.Update(ref state);
            _villagerWealthLookup.Update(ref state);
            _businessBalanceLookup.Update(ref state);
            _villageTreasuryLookup.Update(ref state);

            // Process tax collection requests
            foreach (var (taxRequest, entity) in SystemAPI.Query<RefRO<TaxCollectionRequest>>().WithEntityAccess())
            {
                ProcessTaxCollection(ref state, taxRequest.ValueRO);
                state.EntityManager.RemoveComponent<TaxCollectionRequest>(entity);
            }
        }

        [BurstCompile]
        private void ProcessTaxCollection(ref SystemState state, TaxCollectionRequest request)
        {
            if (!_taxPolicyLookup.HasComponent(request.TaxPolicyEntity))
            {
                return;
            }

            var taxPolicy = _taxPolicyLookup[request.TaxPolicyEntity];
            var treasury = taxPolicy.TargetEntity;

            // Collect income tax from villagers
            if (_villagerWealthLookup.HasComponent(request.Taxpayer))
            {
                var wealth = _villagerWealthLookup[request.Taxpayer];
                float taxAmount = wealth.Balance * 0.1f; // Simplified income tax

                if (treasury != Entity.Null && _villageTreasuryLookup.HasComponent(treasury))
                {
                    WealthTransactionSystem.RecordTransaction(
                        ref state,
                        request.Taxpayer,
                        treasury,
                        taxAmount,
                        TransactionType.Expense,
                        _incomeTaxReason,
                        _taxCollectionChannel
                    );
                }
            }

            // Collect business tax
            if (_businessBalanceLookup.HasComponent(request.Taxpayer))
            {
                var balance = _businessBalanceLookup[request.Taxpayer];
                float taxAmount = balance.Cash * taxPolicy.BusinessProfitTaxRate;

                if (treasury != Entity.Null && _villageTreasuryLookup.HasComponent(treasury))
                {
                    WealthTransactionSystem.RecordTransaction(
                        ref state,
                        request.Taxpayer,
                        treasury,
                        taxAmount,
                        TransactionType.Expense,
                        _businessTaxReason,
                        _taxCollectionChannel
                    );
                }
            }
        }
    }

    /// <summary>
    /// Request to collect taxes from an entity.
    /// </summary>
    public struct TaxCollectionRequest : IComponentData
    {
        public Entity TaxPolicyEntity;
        public Entity Taxpayer;
    }
}

