using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Rendering;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Manages anchored character budget and telemetry.
    /// - Bootstraps budget singleton on first run
    /// - Counts anchored entities each tick
    /// - Cleans up stale entries (dead entities)
    /// - Updates performance telemetry
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AnchorCharacterCommandSystem))]
    public partial struct AnchoredCharacterBudgetSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Only update every 60 ticks (~1 second at 60 TPS) for performance
            if (currentTick % 60 != 0)
            {
                return;
            }

            // Get or create budget singleton
            AnchoredCharacterBudget budget;
            Entity budgetEntity;
            if (!SystemAPI.TryGetSingletonEntity<AnchoredCharacterBudgetTag>(out budgetEntity))
            {
                budgetEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<AnchoredCharacterBudgetTag>(budgetEntity);
                budget = AnchoredCharacterBudget.Default;
                state.EntityManager.AddComponentData(budgetEntity, budget);
            }
            else
            {
                budget = SystemAPI.GetComponent<AnchoredCharacterBudget>(budgetEntity);
            }

            // Count current anchored entities
            int anchoredCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<AnchoredCharacter>>())
            {
                anchoredCount++;
            }

            // Clean up stale player buffer entries
            foreach (var (playerBuffer, playerEntity) in SystemAPI.Query<DynamicBuffer<PlayerAnchoredCharacter>>()
                .WithEntityAccess())
            {
                for (int i = playerBuffer.Length - 1; i >= 0; i--)
                {
                    var entry = playerBuffer[i];
                    
                    // Remove entries for dead entities
                    if (!state.EntityManager.Exists(entry.AnchoredEntity))
                    {
                        playerBuffer.RemoveAt(i);
                        continue;
                    }

                    // Remove entries for entities that lost their anchor component
                    if (!state.EntityManager.HasComponent<AnchoredCharacter>(entry.AnchoredEntity))
                    {
                        playerBuffer.RemoveAt(i);
                    }
                }
            }

            // Update budget telemetry
            budget.TotalCount = anchoredCount;
            budget.LastUpdateTick = currentTick;

            // Telemetry: estimate render/sim cost (placeholder - would integrate with profiler)
            // For now, estimate ~0.5ms per anchored character render, ~0.2ms sim
            budget.RenderCostMs = anchoredCount * 0.5f;
            budget.SimCostMs = anchoredCount * 0.2f;

            SystemAPI.SetComponent(budgetEntity, budget);
        }
    }

    /// <summary>
    /// Helper extensions for querying anchored character budget.
    /// </summary>
    public static class AnchoredCharacterBudgetExtensions
    {
        /// <summary>
        /// Checks if a player can anchor another character.
        /// </summary>
        public static bool CanPlayerAnchor(
            ref SystemState state,
            Entity playerEntity,
            AnchoredCharacterBudget budget)
        {
            if (playerEntity == Entity.Null)
            {
                // Shared anchors have no per-player limit
                return true;
            }

            if (!state.EntityManager.HasBuffer<PlayerAnchoredCharacter>(playerEntity))
            {
                // Player doesn't have buffer yet - can anchor
                return true;
            }

            var buffer = state.EntityManager.GetBuffer<PlayerAnchoredCharacter>(playerEntity);
            return buffer.Length < budget.MaxPerPlayer;
        }

        /// <summary>
        /// Gets the current anchor count for a player.
        /// </summary>
        public static int GetPlayerAnchorCount(
            ref SystemState state,
            Entity playerEntity)
        {
            if (playerEntity == Entity.Null)
            {
                return 0;
            }

            if (!state.EntityManager.HasBuffer<PlayerAnchoredCharacter>(playerEntity))
            {
                return 0;
            }

            return state.EntityManager.GetBuffer<PlayerAnchoredCharacter>(playerEntity).Length;
        }
    }
}

