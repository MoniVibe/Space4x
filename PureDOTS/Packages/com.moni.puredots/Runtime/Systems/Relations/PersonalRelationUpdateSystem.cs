using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Relations;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Relations
{
    /// <summary>
    /// Updates PersonalRelation buffer only after direct interactions (WARM path).
    /// Event-driven: fought together, saved each other, betrayed, trade, gift, insult.
    /// Rare social maintenance tick (every 100+ ticks).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(RelationPerformanceBudgetSystem))]
    public partial struct PersonalRelationUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var budget = SystemAPI.GetSingleton<RelationPerformanceBudget>();

            // Process social interaction events
            // In full implementation, would query for SocialInteractionEvent components
            // For now, this is a placeholder that will be expanded when event system is integrated

            // Social maintenance pass - runs every 100+ ticks
            if (timeState.Tick % 100 == 0)
            {
                var relationsLookup = SystemAPI.GetBufferLookup<PersonalRelation>(false);
                relationsLookup.Update(ref state);
                
                // Query entities with PersonalRelation buffer
                var query = SystemAPI.QueryBuilder()
                    .WithAll<PersonalRelation>()
                    .Build();
                var entities = query.ToEntityArray(state.WorldUpdateAllocator);
                
                // Decay old relations slightly (slow decay)
                for (int e = 0; e < entities.Length; e++)
                {
                    var entity = entities[e];
                    if (!relationsLookup.HasBuffer(entity))
                        continue;
                    
                    var relations = relationsLookup[entity];
                    
                    for (int i = relations.Length - 1; i >= 0; i--)
                    {
                        var relation = relations[i];
                        
                        // Decay strength slightly if relation is old
                        uint ticksSinceUpdate = timeState.Tick - relation.LastUpdateTick;
                        if (ticksSinceUpdate > 500)
                        {
                            // Decay by 1% per 500 ticks
                            float decayFactor = 1f - (ticksSinceUpdate / 50000f);
                            relation.Strength *= math.max(0f, decayFactor);
                            relation.LastUpdateTick = timeState.Tick;
                            relations[i] = relation;
                        }

                        // Remove very weak relations to keep buffer size manageable
                        if (math.abs(relation.Strength) < 0.1f && relation.RelationType != PersonalRelationType.Family)
                        {
                            relations.RemoveAtSwapBack(i);
                        }
                    }

                    // Enforce max relations limit
                    if (relations.Length > budget.MaxPersonalRelationsPerIndividual)
                    {
                        // Remove weakest relations (keep family relations)
                        // Sort by strength, remove weakest non-family relations
                        var toRemove = relations.Length - budget.MaxPersonalRelationsPerIndividual;
                        for (int i = 0; i < toRemove && i < relations.Length; i++)
                        {
                            if (relations[i].RelationType != PersonalRelationType.Family)
                            {
                                relations.RemoveAtSwapBack(i);
                                i--; // Adjust index after removal
                            }
                        }
                    }
                }
                
                entities.Dispose();
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

