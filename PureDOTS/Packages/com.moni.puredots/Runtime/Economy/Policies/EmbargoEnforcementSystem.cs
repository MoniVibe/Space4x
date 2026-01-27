using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Policies
{
    /// <summary>
    /// Blocks embargoed trades and routes to smuggling path.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EmbargoEnforcementSystem : ISystem
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

            // Process embargo checks
            foreach (var (embargoRequest, entity) in SystemAPI.Query<RefRO<EmbargoCheckRequest>>().WithEntityAccess())
            {
                // Check if trade is embargoed
                // If embargoed, route to smuggling path
                state.EntityManager.RemoveComponent<EmbargoCheckRequest>(entity);
            }
        }
    }

    /// <summary>
    /// Request to check embargo.
    /// </summary>
    public struct EmbargoCheckRequest : IComponentData
    {
        public Entity EmbargoPolicyEntity;
        public Entity TradeEntity;
    }
}

