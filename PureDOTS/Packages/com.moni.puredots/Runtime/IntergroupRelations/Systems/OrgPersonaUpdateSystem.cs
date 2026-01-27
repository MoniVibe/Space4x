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
    /// Updates organization persona from member behaviors.
    /// Computes average vengeful/forgiving, bold/craven, and cohesion.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OrgInitSystem))]
    public partial struct OrgPersonaUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            
            var currentTick = timeState.Tick;

            foreach (var (orgId, aggregate, members, memberStats, entity) in SystemAPI.Query<RefRO<OrgId>, RefRO<PureDOTS.Runtime.Aggregate.AggregateEntity>, DynamicBuffer<PureDOTS.Runtime.Aggregate.AggregateMember>, RefRO<PureDOTS.Runtime.Aggregate.AggregateMemberStats>>()
                .WithEntityAccess())
            {
                if (members.Length == 0)
                    continue;

                // Check if org already has persona component
                if (!SystemAPI.HasComponent<OrgPersona>(entity))
                {
                    state.EntityManager.AddComponent<OrgPersona>(entity);
                }

                var persona = SystemAPI.GetComponentRW<OrgPersona>(entity);

                // Compute weighted averages from members
                float totalWeight = 0f;
                float vengefulSum = 0f;
                float boldSum = 0f;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!SystemAPI.Exists(member.MemberEntity))
                        continue;

                    if (!SystemAPI.HasComponent<VillagerBehavior>(member.MemberEntity))
                        continue;

                    var memberBehavior = SystemAPI.GetComponent<VillagerBehavior>(member.MemberEntity);
                    float weight = member.ContributionWeight > 0f ? member.ContributionWeight : 1f;

                    // Convert from sbyte (-100 to +100) to float (0 to 1)
                    // Vengeful: -100 (forgiving) to +100 (vengeful) -> 0 (forgiving) to 1 (vengeful)
                    float vengefulNormalized = math.clamp((memberBehavior.VengefulScore + 100f) / 200f, 0f, 1f);
                    
                    // Bold: -100 (craven) to +100 (bold) -> 0 (craven) to 1 (bold)
                    float boldNormalized = math.clamp((memberBehavior.BoldScore + 100f) / 200f, 0f, 1f);

                    vengefulSum += vengefulNormalized * weight;
                    boldSum += boldNormalized * weight;
                    totalWeight += weight;
                }

                if (totalWeight > 0f)
                {
                    persona.ValueRW.VengefulForgiving = math.clamp(vengefulSum / totalWeight, 0f, 1f);
                    persona.ValueRW.CravenBold = math.clamp(boldSum / totalWeight, 0f, 1f);
                    persona.ValueRW.Cohesion = memberStats.ValueRO.Cohesion; // Use cohesion from aggregate stats
                    persona.ValueRW.LastUpdateTick = currentTick;
                }
            }
        }
    }
}

