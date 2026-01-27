using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Wealth
{
    /// <summary>
    /// Records wealth transactions and applies balance changes.
    /// Ensures all wealth changes go through explicit transaction records.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WealthTransactionSystem : ISystem
    {
        private ComponentLookup<VillagerWealth> _villagerWealthLookup;
        private ComponentLookup<FamilyWealth> _familyWealthLookup;
        private ComponentLookup<DynastyWealth> _dynastyWealthLookup;
        private ComponentLookup<BusinessBalance> _businessBalanceLookup;
        private ComponentLookup<GuildTreasury> _guildTreasuryLookup;
        private ComponentLookup<VillageTreasury> _villageTreasuryLookup;
        private BufferLookup<WealthTransaction> _transactionBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _villagerWealthLookup = state.GetComponentLookup<VillagerWealth>(false);
            _familyWealthLookup = state.GetComponentLookup<FamilyWealth>(false);
            _dynastyWealthLookup = state.GetComponentLookup<DynastyWealth>(false);
            _businessBalanceLookup = state.GetComponentLookup<BusinessBalance>(false);
            _guildTreasuryLookup = state.GetComponentLookup<GuildTreasury>(false);
            _villageTreasuryLookup = state.GetComponentLookup<VillageTreasury>(false);
            _transactionBufferLookup = state.GetBufferLookup<WealthTransaction>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) &&
                (!scenario.IsInitialized || !scenario.EnableEconomy))
            {
                return;
            }

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var tick = tickTimeState.Tick;

            _villagerWealthLookup.Update(ref state);
            _familyWealthLookup.Update(ref state);
            _dynastyWealthLookup.Update(ref state);
            _businessBalanceLookup.Update(ref state);
            _guildTreasuryLookup.Update(ref state);
            _villageTreasuryLookup.Update(ref state);
            _transactionBufferLookup.Update(ref state);

            // Process pending transactions from the ledger
            var ledgerEntity = GetOrCreateLedgerEntity(ref state);
            if (_transactionBufferLookup.HasBuffer(ledgerEntity))
            {
                var transactions = _transactionBufferLookup[ledgerEntity];
                var transactionsToProcess = new NativeList<WealthTransaction>(Allocator.Temp);
                
                for (int i = 0; i < transactions.Length; i++)
                {
                    var transaction = transactions[i];
                    if (transaction.Tick == tick || transaction.Tick == 0)
                    {
                        // Mark for processing
                        if (transaction.Tick == 0)
                        {
                            transaction.Tick = tick;
                            transactions[i] = transaction;
                        }
                        transactionsToProcess.Add(transaction);
                    }
                }

                // Apply all transactions
                for (int i = 0; i < transactionsToProcess.Length; i++)
                {
                    ApplyTransaction(ref state, transactionsToProcess[i], tick);
                }

                transactionsToProcess.Dispose();
            }
        }

        [BurstCompile]
        private void ApplyTransaction(ref SystemState state, WealthTransaction transaction, uint tick)
        {
            // Update source wallet (decrease balance)
            UpdateWalletBalance(ref state, transaction.From, -transaction.Amount, transaction.Reason, tick);

            // Update destination wallet (increase balance)
            UpdateWalletBalance(ref state, transaction.To, transaction.Amount, transaction.Reason, tick);
        }

        [BurstCompile]
        private void UpdateWalletBalance(ref SystemState state, Entity entity, float amount, FixedString64Bytes reason, uint tick)
        {
            if (entity == Entity.Null)
            {
                return;
            }

            if (_villagerWealthLookup.HasComponent(entity))
            {
                var wealth = _villagerWealthLookup[entity];
                wealth.Balance += amount;
                wealth.LastChangeSource = reason;
                wealth.LastUpdateTick = tick;
                _villagerWealthLookup[entity] = wealth;
            }
            else if (_familyWealthLookup.HasComponent(entity))
            {
                var wealth = _familyWealthLookup[entity];
                wealth.Balance += amount;
                wealth.LastChangeSource = reason;
                wealth.LastUpdateTick = tick;
                _familyWealthLookup[entity] = wealth;
            }
            else if (_dynastyWealthLookup.HasComponent(entity))
            {
                var wealth = _dynastyWealthLookup[entity];
                wealth.Balance += amount;
                wealth.LastChangeSource = reason;
                wealth.LastUpdateTick = tick;
                _dynastyWealthLookup[entity] = wealth;
            }
            else if (_businessBalanceLookup.HasComponent(entity))
            {
                var balance = _businessBalanceLookup[entity];
                balance.Cash += amount;
                balance.LastChangeSource = reason;
                balance.LastUpdateTick = tick;
                _businessBalanceLookup[entity] = balance;
            }
            else if (_guildTreasuryLookup.HasComponent(entity))
            {
                var treasury = _guildTreasuryLookup[entity];
                treasury.Balance += amount;
                treasury.LastChangeSource = reason;
                treasury.LastUpdateTick = tick;
                _guildTreasuryLookup[entity] = treasury;
            }
            else if (_villageTreasuryLookup.HasComponent(entity))
            {
                var treasury = _villageTreasuryLookup[entity];
                treasury.Balance += amount;
                treasury.LastChangeSource = reason;
                treasury.LastUpdateTick = tick;
                _villageTreasuryLookup[entity] = treasury;
            }
        }

        /// <summary>
        /// Records a transaction between two entities.
        /// Transaction will be applied by WealthTransactionSystem on next update.
        /// Call this helper to create transactions from other systems.
        /// </summary>
        public static void RecordTransaction(ref SystemState state, Entity from, Entity to, float amount, TransactionType type, FixedString64Bytes reason, FixedString128Bytes context = default)
        {
            // Find or create a ledger entity to store transactions
            var ledgerEntity = GetOrCreateLedgerEntity(ref state);

            if (!state.EntityManager.HasBuffer<WealthTransaction>(ledgerEntity))
            {
                state.EntityManager.AddBuffer<WealthTransaction>(ledgerEntity);
            }

            var transaction = new WealthTransaction
            {
                From = from,
                To = to,
                Amount = amount,
                Type = type,
                Reason = reason,
                Tick = 0, // Will be set by system on next update
                Context = context
            };

            // Add to ledger (will be processed next frame)
            var transactions = state.EntityManager.GetBuffer<WealthTransaction>(ledgerEntity);
            transactions.Add(transaction);
        }

        private static Entity GetOrCreateLedgerEntity(ref SystemState state)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<WealthLedger>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            var ledgerEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<WealthLedger>(ledgerEntity);
            return ledgerEntity;
        }
    }

    /// <summary>
    /// Tag component for the global wealth ledger entity.
    /// </summary>
    public struct WealthLedger : IComponentData
    {
    }
}

