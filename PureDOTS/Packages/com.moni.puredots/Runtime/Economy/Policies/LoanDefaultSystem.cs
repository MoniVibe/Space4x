using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime.Economy.Wealth;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Policies
{
    /// <summary>
    /// Handles loan defaults: asset seizure via Chunk 2, tribute, vassalization.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct LoanDefaultSystem : ISystem
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

            // Process default requests
            foreach (var (defaultRequest, entity) in SystemAPI.Query<RefRO<LoanDefaultRequest>>().WithEntityAccess())
            {
                // Handle default: asset seizure, tribute, vassalization
                // Simplified - should use Chunk 2 inventory operations for asset seizure
                state.EntityManager.RemoveComponent<LoanDefaultRequest>(entity);
            }
        }
    }

    /// <summary>
    /// Request to handle loan default.
    /// </summary>
    public struct LoanDefaultRequest : IComponentData
    {
        public Entity LoanEntity;
        public Entity Borrower;
        public Entity Lender;
    }
}

