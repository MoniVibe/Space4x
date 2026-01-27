using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;

namespace PureDOTS.Systems.Relations
{
    /// <summary>
    /// System that processes relation formation events and creates personal relations.
    /// Event-driven: processes RelationFormationEvent buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(RelationUpdateSystem))]
    public partial struct PersonalRelationFormationSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var relationsLookup = SystemAPI.GetBufferLookup<PersonalRelation>(false);
            var entityRelationsLookup = SystemAPI.GetBufferLookup<EntityRelation>(true);
            relationsLookup.Update(ref state);
            entityRelationsLookup.Update(ref state);

            // Process relation formation events
            foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<RelationFormationEvent>>().WithEntityAccess())
            {
                if (!relationsLookup.HasBuffer(entity))
                    continue;

                var personalRelations = relationsLookup[entity];
                float relationScore = 0f;

                // Get relation score from EntityRelation if available
                if (entityRelationsLookup.HasBuffer(entity))
                {
                    var entityRelations = entityRelationsLookup[entity];
                    for (int i = 0; i < events.Length; i++)
                    {
                        var evt = events[i];
                        int relationIndex = RelationCalculator.FindRelationIndex(entityRelations, evt.TargetEntity);
                        if (relationIndex >= 0)
                        {
                            relationScore = entityRelations[relationIndex].Intensity;
                        }

                        // Process formation event
                        PersonalRelationFormationService.ProcessFormationEvent(
                            ref personalRelations,
                            evt.TargetEntity,
                            evt.EventType,
                            relationScore,
                            currentTick);
                    }
                }
                else
                {
                    // Process without relation score (use default)
                    for (int i = 0; i < events.Length; i++)
                    {
                        var evt = events[i];
                        PersonalRelationFormationService.ProcessFormationEvent(
                            ref personalRelations,
                            evt.TargetEntity,
                            evt.EventType,
                            0f, // Default score
                            currentTick);
                    }
                }

                // Clear processed events
                events.Clear();
            }
        }
    }
}

