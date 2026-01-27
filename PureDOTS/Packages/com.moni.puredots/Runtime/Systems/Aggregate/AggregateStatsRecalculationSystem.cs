using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Motivation;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Aggregate
{
    /// <summary>
    /// Recomputes aggregate statistics by averaging member traits when groups are marked dirty.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GroupMembershipChangeSystem))]
    public partial struct AggregateStatsRecalculationSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }

            // Skip if paused or rewinding
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            // Process groups marked as dirty
            foreach (var (identity, stats, entity) in SystemAPI.Query<
                RefRO<AggregateIdentity>,
                RefRW<AggregateStats>>()
                .WithAll<AggregateStatsDirtyTag>()
                .WithEntityAccess())
            {
                var groupEntity = entity;
                var statsValue = stats.ValueRO;

                // Accumulate member traits
                float sumInitiative = 0f;
                float sumVengefulForgiving = 0f;
                float sumBoldCraven = 0f;
                float sumCorruptPure = 0f;
                float sumChaoticLawful = 0f;
                float sumEvilGood = 0f;
                float sumMightMagic = 0f;
                float sumAmbition = 0f;
                float sumDesireStatus = 0f;
                float sumDesireWealth = 0f;
                float sumDesirePower = 0f;
                float sumDesireKnowledge = 0f;
                int memberCount = 0;

                // Query all entities with GroupMembership pointing to this group
                foreach (var (memberProfile, memberMembership) in SystemAPI.Query<
                    RefRO<MoralProfile>,
                    RefRO<GroupMembership>>())
                {
                    if (memberMembership.ValueRO.Group != groupEntity)
                        continue;

                    var profile = memberProfile.ValueRO;
                    memberCount++;

                    // Accumulate traits
                    sumInitiative += profile.Initiative;
                    sumVengefulForgiving += profile.VengefulForgiving;
                    sumBoldCraven += profile.CravenBold; // Note: CravenBold in profile
                    sumCorruptPure += profile.CorruptPure;
                    sumChaoticLawful += profile.ChaoticLawful;
                    sumEvilGood += profile.EvilGood;
                    sumMightMagic += profile.MightMagic;
                    sumAmbition += profile.Ambition;
                    sumDesireStatus += profile.DesireStatus;
                    sumDesireWealth += profile.DesireWealth;
                    sumDesirePower += profile.DesirePower;
                    sumDesireKnowledge += profile.DesireKnowledge;
                }

                // Calculate averages
                if (memberCount > 0)
                {
                    statsValue.AvgInitiative = sumInitiative / memberCount;
                    statsValue.AvgVengefulForgiving = sumVengefulForgiving / memberCount;
                    statsValue.AvgBoldCraven = sumBoldCraven / memberCount;
                    statsValue.AvgCorruptPure = sumCorruptPure / memberCount;
                    statsValue.AvgChaoticLawful = sumChaoticLawful / memberCount;
                    statsValue.AvgEvilGood = sumEvilGood / memberCount;
                    statsValue.AvgMightMagic = sumMightMagic / memberCount;
                    statsValue.AvgAmbition = sumAmbition / memberCount;
                    statsValue.StatusCoverage = sumDesireStatus / memberCount;
                    statsValue.WealthCoverage = sumDesireWealth / memberCount;
                    statsValue.PowerCoverage = sumDesirePower / memberCount;
                    statsValue.KnowledgeCoverage = sumDesireKnowledge / memberCount;
                }
                else
                {
                    // No members - zero out stats
                    statsValue.AvgInitiative = 0f;
                    statsValue.AvgVengefulForgiving = 0f;
                    statsValue.AvgBoldCraven = 0f;
                    statsValue.AvgCorruptPure = 0f;
                    statsValue.AvgChaoticLawful = 0f;
                    statsValue.AvgEvilGood = 0f;
                    statsValue.AvgMightMagic = 0f;
                    statsValue.AvgAmbition = 0f;
                    statsValue.StatusCoverage = 0f;
                    statsValue.WealthCoverage = 0f;
                    statsValue.PowerCoverage = 0f;
                    statsValue.KnowledgeCoverage = 0f;
                }

                statsValue.MemberCount = memberCount;
                statsValue.LastRecalcTick = currentTick;
                stats.ValueRW = statsValue;

                // Remove dirty tag
                ecb.RemoveComponent<AggregateStatsDirtyTag>(groupEntity);
            }
        }
    }
}
























