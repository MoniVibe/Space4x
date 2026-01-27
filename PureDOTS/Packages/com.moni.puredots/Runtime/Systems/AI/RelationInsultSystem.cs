using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using PureDOTS.Systems.Relations;
using Unity.Mathematics;
using static PureDOTS.Runtime.Social.InteractionOutcome;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// System that makes entities insult/swear at very bad relations (-80 to -100).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(RelationUpdateSystem))]
    public partial struct RelationInsultSystem : ISystem
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

            var relationsLookup = SystemAPI.GetBufferLookup<EntityRelation>(true);
            relationsLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Query entities with very bad relations
            foreach (var (relations, entity) in SystemAPI.Query<DynamicBuffer<EntityRelation>>().WithEntityAccess())
            {
                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i];
                    
                    // Very bad relations (-80 to -100): Emit insults
                    if (relation.Intensity <= -80)
                    {
                        // Emit insult event
                        if (SystemAPI.HasBuffer<InsultEvent>(entity))
                        {
                            var insults = SystemAPI.GetBuffer<InsultEvent>(entity);
                            insults.Add(new InsultEvent
                            {
                                Target = relation.OtherEntity,
                                Type = InsultType.Verbal,
                                Severity = (byte)math.clamp(math.abs(relation.Intensity) / 10, 1, 10),
                                Tick = currentTick
                            });
                        }

                        // Record negative interaction (insults worsen relations)
                        var requestEntity = ecb.CreateEntity();
                        ecb.AddComponent(requestEntity, new RecordInteractionRequest
                        {
                            EntityA = entity,
                            EntityB = relation.OtherEntity,
                            Outcome = InteractionOutcome.Negative,
                            IntensityChange = -1, // Small negative delta
                            TrustChange = -1,
                            IsMutual = false
                        });
                    }
                }
            }

        }
    }
}

