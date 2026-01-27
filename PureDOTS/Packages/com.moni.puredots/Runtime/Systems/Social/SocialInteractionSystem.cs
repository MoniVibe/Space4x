using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Social;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Social
{
    /// <summary>
    /// Handles marriages, friendships, alliances from candidate lists (WARM path).
    /// Draws from candidate lists, filters by alignment/outlook/personality compatibility.
    /// Respects budget.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(SocialCandidateListSystem))]
    public partial struct SocialInteractionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<RelationPerformanceBudget>();
            state.RequireForUpdate<RelationPerformanceCounters>();
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

            var budget = SystemAPI.GetSingleton<RelationPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<RelationPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.SocialInteractionsThisTick >= budget.MaxSocialInteractionsPerTick)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Process social interaction requests
            // In full implementation, would query for SocialInteractionRequest components
            // For now, this is a placeholder that processes from candidate lists

            // Example: Process marriage requests
            foreach (var (candidateList, candidates, entity) in
                SystemAPI.Query<RefRO<SocialCandidateList>, DynamicBuffer<SocialCandidate>>()
                .WithEntityAccess())
            {
                if (counters.ValueRO.SocialInteractionsThisTick >= budget.MaxSocialInteractionsPerTick)
                {
                    break;
                }

                // Find best candidates for each category
                for (int category = 0; category < 5; category++) // 5 categories
                {
                    if (counters.ValueRO.SocialInteractionsThisTick >= budget.MaxSocialInteractionsPerTick)
                    {
                        break;
                    }

                    // Find best candidate in this category
                    Entity bestCandidate = Entity.Null;
                    float bestScore = 0f;

                    for (int i = 0; i < candidates.Length; i++)
                    {
                        var candidate = candidates[i];
                        if (candidate.Category == (SocialCandidateCategory)category)
                        {
                            if (candidate.CompatibilityScore > bestScore)
                            {
                                bestScore = candidate.CompatibilityScore;
                                bestCandidate = candidate.CandidateEntity;
                            }
                        }
                    }

                    // In full implementation, would create social relationship based on best candidate
                    // For now, just increment counter
                    counters.ValueRW.SocialInteractionsThisTick++;
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

