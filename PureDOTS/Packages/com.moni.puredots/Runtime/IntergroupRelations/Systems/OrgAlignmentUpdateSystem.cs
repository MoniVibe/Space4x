using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Villagers;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.IntergroupRelations
{
    /// <summary>
    /// Updates organization alignment from member alignments.
    /// Computes weighted average alignment from all members.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // Note: AggregateStatsRecalculationSystem is in PureDOTS.Systems assembly which Runtime can't reference
    // [UpdateAfter(typeof(AggregateStatsRecalculationSystem))]
    public partial struct OrgAlignmentUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (orgId, aggregate, members, entity) in SystemAPI.Query<RefRO<OrgId>, RefRO<PureDOTS.Runtime.Aggregate.AggregateEntity>, DynamicBuffer<PureDOTS.Runtime.Aggregate.AggregateMember>>()
                .WithEntityAccess())
            {
                if (members.Length == 0)
                    continue;

                // Check if org already has alignment component
                if (!SystemAPI.HasComponent<VillagerAlignment>(entity))
                {
                    state.EntityManager.AddComponent<VillagerAlignment>(entity);
                }

                var alignment = SystemAPI.GetComponentRW<VillagerAlignment>(entity);

                // Compute weighted average alignment from members
                float totalWeight = 0f;
                float3 weightedMoral = float3.zero;
                float3 weightedOrder = float3.zero;
                float3 weightedPurity = float3.zero;
                float totalAlignmentStrength = 0f;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!SystemAPI.Exists(member.MemberEntity))
                        continue;

                    if (!SystemAPI.HasComponent<VillagerAlignment>(member.MemberEntity))
                        continue;

                    var memberAlignment = SystemAPI.GetComponent<VillagerAlignment>(member.MemberEntity);
                    float weight = member.ContributionWeight > 0f ? member.ContributionWeight : 1f;

                    weightedMoral.x += memberAlignment.MoralAxis * weight;
                    weightedOrder.x += memberAlignment.OrderAxis * weight;
                    weightedPurity.x += memberAlignment.PurityAxis * weight;
                    totalAlignmentStrength += memberAlignment.AlignmentStrength * weight;
                    totalWeight += weight;
                }

                if (totalWeight > 0f)
                {
                    alignment.ValueRW.MoralAxis = (sbyte)math.clamp(weightedMoral.x / totalWeight, -100, 100);
                    alignment.ValueRW.OrderAxis = (sbyte)math.clamp(weightedOrder.x / totalWeight, -100, 100);
                    alignment.ValueRW.PurityAxis = (sbyte)math.clamp(weightedPurity.x / totalWeight, -100, 100);
                    alignment.ValueRW.AlignmentStrength = math.clamp(totalAlignmentStrength / totalWeight, 0f, 1f);
                }
            }
        }
    }
}

