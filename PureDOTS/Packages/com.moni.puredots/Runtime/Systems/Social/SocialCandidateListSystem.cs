using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Social;
using PureDOTS.Systems.Relations;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Social
{
    /// <summary>
    /// Maintains social candidate lists (WARM path).
    /// Updates every 100+ ticks, rebuilds only when population changes significantly.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(RelationPerformanceBudgetSystem))]
    public partial struct SocialCandidateListSystem : ISystem
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

            // Update candidate lists every 100+ ticks
            foreach (var (candidateList, candidates, entity) in
                SystemAPI.Query<RefRW<SocialCandidateList>, DynamicBuffer<SocialCandidate>>()
                .WithEntityAccess())
            {
                // Check if update is needed
                uint ticksSinceUpdate = timeState.Tick - candidateList.ValueRO.LastUpdateTick;
                if (ticksSinceUpdate < 100 && candidateList.ValueRO.NeedsRebuild == 0)
                {
                    continue;
                }

                // Rebuild candidate list if needed
                if (candidateList.ValueRO.NeedsRebuild != 0 || ticksSinceUpdate > 500)
                {
                    // Clear existing candidates
                    candidates.Clear();

                    // In full implementation, would:
                    // 1. Query entities in same location
                    // 2. Filter by culture/faction, status, age eligibility
                    // 3. Calculate compatibility scores
                    // 4. Add top N candidates per category

                    candidateList.ValueRW.NeedsRebuild = 0;
                }

                candidateList.ValueRW.LastUpdateTick = timeState.Tick;
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

