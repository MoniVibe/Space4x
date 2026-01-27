using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Wealth
{
    /// <summary>
    /// Computes aggregate wealth for families and dynasties.
    /// Family wealth = FamilyWallet.Balance + Sum(MemberWealth.Balance)
    /// Dynasty wealth = DynastyWallet.Balance + Sum(FamilyWealthAggregate for all member families)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WealthAggregationSystem : ISystem
    {
        private ComponentLookup<VillagerWealth> _villagerWealthLookup;
        private ComponentLookup<FamilyWealth> _familyWealthLookup;
        private ComponentLookup<DynastyWealth> _dynastyWealthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _villagerWealthLookup = state.GetComponentLookup<VillagerWealth>(true);
            _familyWealthLookup = state.GetComponentLookup<FamilyWealth>(true);
            _dynastyWealthLookup = state.GetComponentLookup<DynastyWealth>(true);
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

            _villagerWealthLookup.Update(ref state);
            _familyWealthLookup.Update(ref state);
            _dynastyWealthLookup.Update(ref state);

            // Update family aggregate components
            foreach (var (aggregate, entity) in SystemAPI.Query<RefRW<FamilyWealthAggregate>>().WithEntityAccess())
            {
                UpdateFamilyAggregate(ref state, entity, ref aggregate.ValueRW);
            }

            // Update dynasty aggregate components
            foreach (var (aggregate, entity) in SystemAPI.Query<RefRW<DynastyWealthAggregate>>().WithEntityAccess())
            {
                UpdateDynastyAggregate(ref state, entity, ref aggregate.ValueRW);
            }
        }

        [BurstCompile]
        private void UpdateFamilyAggregate(ref SystemState state, Entity familyEntity, ref FamilyWealthAggregate aggregate)
        {
            float totalWealth = 0f;

            // Add family wallet balance if it exists
            if (_familyWealthLookup.HasComponent(familyEntity))
            {
                totalWealth += _familyWealthLookup[familyEntity].Balance;
            }

            // Sum member wealth
            // TODO: Query family membership relationships when available
            // For now, this is a placeholder that will be extended when family relationships are implemented

            aggregate.TotalWealth = totalWealth;
        }

        [BurstCompile]
        private void UpdateDynastyAggregate(ref SystemState state, Entity dynastyEntity, ref DynastyWealthAggregate aggregate)
        {
            float totalWealth = 0f;

            // Add dynasty wallet balance if it exists
            if (_dynastyWealthLookup.HasComponent(dynastyEntity))
            {
                totalWealth += _dynastyWealthLookup[dynastyEntity].Balance;
            }

            // Sum family aggregate wealth
            // TODO: Query dynasty membership relationships when available
            // For now, this is a placeholder that will be extended when dynasty relationships are implemented

            aggregate.TotalWealth = totalWealth;
        }
    }

    /// <summary>
    /// Computed aggregate wealth for a family.
    /// Updated by WealthAggregationSystem.
    /// </summary>
    public struct FamilyWealthAggregate : IComponentData
    {
        public float TotalWealth;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Computed aggregate wealth for a dynasty.
    /// Updated by WealthAggregationSystem.
    /// </summary>
    public struct DynastyWealthAggregate : IComponentData
    {
        public float TotalWealth;
        public uint LastUpdateTick;
    }
}

