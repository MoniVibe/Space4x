using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Wealth
{
    /// <summary>
    /// Recomputes wealth tiers for all wallets based on their balances.
    /// Runs periodically (configurable cadence) to update tiers from WealthTierSpec catalog.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WealthTierUpdateSystem : ISystem
    {
        private ComponentLookup<VillagerWealth> _villagerWealthLookup;
        private ComponentLookup<FamilyWealth> _familyWealthLookup;
        private ComponentLookup<DynastyWealth> _dynastyWealthLookup;
        private ComponentLookup<BusinessBalance> _businessBalanceLookup;
        private ComponentLookup<GuildTreasury> _guildTreasuryLookup;
        private ComponentLookup<VillageTreasury> _villageTreasuryLookup;

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

            // Check if we have a tier catalog
            if (!SystemAPI.TryGetSingleton<WealthTierSpecCatalog>(out var catalog))
            {
                return;
            }

            ref var catalogBlob = ref catalog.Catalog.Value;
            if (catalogBlob.Tiers.Length == 0)
            {
                return;
            }

            _villagerWealthLookup.Update(ref state);
            _familyWealthLookup.Update(ref state);
            _dynastyWealthLookup.Update(ref state);
            _businessBalanceLookup.Update(ref state);
            _guildTreasuryLookup.Update(ref state);
            _villageTreasuryLookup.Update(ref state);

            // Update tiers for all wallet types
            foreach (var (wealth, entity) in SystemAPI.Query<RefRW<VillagerWealth>>().WithEntityAccess())
            {
                var balance = wealth.ValueRO.Balance;
                var newTier = ComputeTier(balance, ref catalogBlob);
                if (wealth.ValueRO.Tier != newTier)
                {
                    wealth.ValueRW.Tier = newTier;
                }
            }

            foreach (var (wealth, entity) in SystemAPI.Query<RefRW<FamilyWealth>>().WithEntityAccess())
            {
                var balance = wealth.ValueRO.Balance;
                var newTier = ComputeTier(balance, ref catalogBlob);
                if (wealth.ValueRO.Tier != newTier)
                {
                    wealth.ValueRW.Tier = newTier;
                }
            }

            foreach (var (wealth, entity) in SystemAPI.Query<RefRW<DynastyWealth>>().WithEntityAccess())
            {
                var balance = wealth.ValueRO.Balance;
                var newTier = ComputeTier(balance, ref catalogBlob);
                if (wealth.ValueRO.Tier != newTier)
                {
                    wealth.ValueRW.Tier = newTier;
                }
            }

            foreach (var (balance, entity) in SystemAPI.Query<RefRW<BusinessBalance>>().WithEntityAccess())
            {
                var cash = balance.ValueRO.Cash;
                var newTier = ComputeTier(cash, ref catalogBlob);
                if (balance.ValueRO.Tier != newTier)
                {
                    balance.ValueRW.Tier = newTier;
                }
            }

            foreach (var (treasury, entity) in SystemAPI.Query<RefRW<GuildTreasury>>().WithEntityAccess())
            {
                var balance = treasury.ValueRO.Balance;
                var newTier = ComputeTier(balance, ref catalogBlob);
                if (treasury.ValueRO.Tier != newTier)
                {
                    treasury.ValueRW.Tier = newTier;
                }
            }

            foreach (var (treasury, entity) in SystemAPI.Query<RefRW<VillageTreasury>>().WithEntityAccess())
            {
                var balance = treasury.ValueRO.Balance;
                var newTier = ComputeTier(balance, ref catalogBlob);
                if (treasury.ValueRO.Tier != newTier)
                {
                    treasury.ValueRW.Tier = newTier;
                }
            }
        }

        [BurstCompile]
        private static WealthTier ComputeTier(float balance, ref WealthTierSpecCatalogBlob catalog)
        {
            for (int i = 0; i < catalog.Tiers.Length; i++)
            {
                var tier = catalog.Tiers[i];
                if (balance >= tier.MinWealth && (tier.MaxWealth < 0 || balance < tier.MaxWealth))
                {
                    // Map tier index to WealthTier enum
                    if (i < 5)
                    {
                        return (WealthTier)i;
                    }
                    return WealthTier.UltraHigh;
                }
            }

            // Default to lowest tier if no match
            return WealthTier.UltraPoor;
        }
    }
}

