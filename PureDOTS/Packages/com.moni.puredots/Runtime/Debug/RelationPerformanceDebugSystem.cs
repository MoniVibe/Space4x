using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Relations;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Debugging
{
    /// <summary>
    /// Displays relation/econ/social performance counters and budget warnings in a debug overlay.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct RelationPerformanceDebugSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RelationPerformanceBudget>();
            state.RequireForUpdate<RelationPerformanceCounters>();
            state.RequireForUpdate<DebugDisplayData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var budget = SystemAPI.GetSingleton<RelationPerformanceBudget>();
            var counters = SystemAPI.GetSingleton<RelationPerformanceCounters>();
            var debugDisplay = SystemAPI.GetSingletonRW<DebugDisplayData>();

            // Build relation/econ performance data string
            var text = new Unity.Collections.FixedString512Bytes();
            text.Append("--- Relation/Econ Performance ---\n");
            text.Append("Relation Events: ");
            text.Append(counters.RelationEventsThisTick);
            text.Append("/");
            text.Append(budget.MaxRelationEventsPerTick);
            text.Append("\n");
            
            text.Append("Market Updates: ");
            text.Append(counters.MarketUpdatesThisTick);
            text.Append("/");
            text.Append(budget.MaxMarketUpdatesPerTick);
            text.Append("\n");
            
            text.Append("Political Decisions: ");
            text.Append(counters.PoliticalDecisionsThisTick);
            text.Append("/");
            text.Append(budget.MaxPoliticalDecisionsPerTick);
            text.Append("\n");
            
            text.Append("Social Interactions: ");
            text.Append(counters.SocialInteractionsThisTick);
            text.Append("/");
            text.Append(budget.MaxSocialInteractionsPerTick);
            text.Append("\n");
            
            text.Append("Personal Relations: ");
            text.Append(counters.TotalPersonalRelations);
            text.Append(" (Max: ");
            text.Append(budget.MaxPersonalRelationsPerIndividual);
            text.Append(")\n");
            
            text.Append("Org Relations: ");
            text.Append(counters.TotalOrgRelations);
            text.Append(" (Max: ");
            text.Append(budget.MaxOrgRelationsPerOrg);
            text.Append(")\n");
            
            text.Append("Operations Dropped: ");
            text.Append(counters.OperationsDroppedThisTick);
            text.Append("\n");

            // Warn if budgets exceeded
            if (counters.RelationEventsThisTick >= budget.MaxRelationEventsPerTick ||
                counters.MarketUpdatesThisTick >= budget.MaxMarketUpdatesPerTick ||
                counters.PoliticalDecisionsThisTick >= budget.MaxPoliticalDecisionsPerTick ||
                counters.SocialInteractionsThisTick >= budget.MaxSocialInteractionsPerTick)
            {
                text.Append("<color=yellow>WARNING: Budget Exceeded!</color>\n");
            }

            // Warn if graph sizes too large
            if (counters.TotalPersonalRelations > budget.RelationGraphWarningThreshold)
            {
                text.Append("<color=yellow>WARNING: Personal Relations Graph Large (");
                text.Append(counters.TotalPersonalRelations);
                text.Append(")</color>\n");
            }

            // TODO: PerformanceDebugText field needs to be added to DebugDisplayData
            // debugDisplay.ValueRW.PerformanceDebugText = text;
        }
    }
}
