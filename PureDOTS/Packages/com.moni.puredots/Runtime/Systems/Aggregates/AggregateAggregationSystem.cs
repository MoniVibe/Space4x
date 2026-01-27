using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Aggregates generic member stats into the shared AggregateEntity fields.
    /// Category-specific systems can run before this to maintain membership buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AggregateAggregationSystem : ISystem
    {
        private ComponentLookup<AggregateMemberStats> _memberStatsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _memberStatsLookup = state.GetComponentLookup<AggregateMemberStats>(true);
            state.RequireForUpdate<AggregateEntity>();
            state.RequireForUpdate<AggregateMember>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _memberStatsLookup.Update(ref state);

            foreach (var (aggregateRef, members) in SystemAPI
                         .Query<RefRW<AggregateEntity>, DynamicBuffer<AggregateMember>>())
            {
                ref var aggregate = ref aggregateRef.ValueRW;
                if (members.Length == 0)
                {
                    aggregate.MemberCount = 0;
                    aggregate.Morale = 0f;
                    aggregate.Cohesion = 0f;
                    aggregate.Stress = 0f;
                    continue;
                }

                float weightSum = 0f;
                float moraleSum = 0f;
                float cohesionSum = 0f;
                float stressSum = 0f;
                float wealthSum = 0f;
                float reputationSum = 0f;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!_memberStatsLookup.HasComponent(member.Member))
                    {
                        continue;
                    }

                    var stats = _memberStatsLookup[member.Member];
                    var weight = member.Weight > 0f ? member.Weight : 1f;
                    weightSum += weight;
                    moraleSum += stats.Morale * weight;
                    cohesionSum += stats.Cohesion * weight;
                    stressSum += stats.Stress * weight;
                    wealthSum += stats.WealthContribution;
                    reputationSum += stats.ReputationContribution;
                }

                if (weightSum <= math.FLT_MIN_NORMAL)
                {
                    aggregate.MemberCount = members.Length;
                    aggregate.Morale = 0f;
                    aggregate.Cohesion = 0f;
                    aggregate.Stress = 0f;
                }
                else
                {
                    aggregate.MemberCount = (int)math.round(weightSum);
                    aggregate.Morale = moraleSum / weightSum;
                    aggregate.Cohesion = cohesionSum / weightSum;
                    aggregate.Stress = stressSum / weightSum;
                }

                aggregate.Wealth = wealthSum;
                aggregate.Reputation = reputationSum;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
