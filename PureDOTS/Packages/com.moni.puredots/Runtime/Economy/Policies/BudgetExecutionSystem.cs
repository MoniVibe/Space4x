using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Policies
{
    /// <summary>
    /// Allocates treasury to systems via funding multipliers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BudgetExecutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
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

            // Process budget allocation requests
            foreach (var (budgetRequest, entity) in SystemAPI.Query<RefRO<BudgetAllocationRequest>>().WithEntityAccess())
            {
                // Allocate funds based on budget policy
                // Simplified - should set funding multipliers for systems
                state.EntityManager.RemoveComponent<BudgetAllocationRequest>(entity);
            }
        }
    }

    /// <summary>
    /// Request to allocate budget.
    /// </summary>
    public struct BudgetAllocationRequest : IComponentData
    {
        public Entity BudgetPolicyEntity;
        public Entity TreasuryEntity;
    }
}

