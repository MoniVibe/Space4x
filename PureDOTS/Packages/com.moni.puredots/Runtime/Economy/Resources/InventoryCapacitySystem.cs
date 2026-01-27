using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Resources
{
    /// <summary>
    /// Enforces inventory capacity limits (MaxMass, MaxVolume).
    /// Flags over-capacity inventories for debug visibility.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InventoryCapacitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Optional: skip until scenario world is ready
            if (SystemAPI.TryGetSingleton<ScenarioState>(out var scenarioState) &&
                (!scenarioState.IsInitialized || !scenarioState.EnableEconomy))
            {
                return;
            }

            foreach (var (inventory, entity) in SystemAPI.Query<RefRW<Inventory>>().WithEntityAccess())
            {
                var inv = inventory.ValueRO;
                bool overCapacity = false;

                if (inv.CurrentMass > inv.MaxMass)
                {
                    overCapacity = true;
                }

                if (inv.MaxVolume > 0f && inv.CurrentVolume > inv.MaxVolume)
                {
                    overCapacity = true;
                }

                // Add/remove over-capacity flag component
                if (overCapacity && !state.EntityManager.HasComponent<InventoryOverCapacity>(entity))
                {
                    state.EntityManager.AddComponent<InventoryOverCapacity>(entity);
                }
                else if (!overCapacity && state.EntityManager.HasComponent<InventoryOverCapacity>(entity))
                {
                    state.EntityManager.RemoveComponent<InventoryOverCapacity>(entity);
                }
            }
        }
    }

    /// <summary>
    /// Tag component indicating inventory is over capacity.
    /// Added by InventoryCapacitySystem for debug visibility.
    /// </summary>
    public struct InventoryOverCapacity : IComponentData
    {
    }
}
