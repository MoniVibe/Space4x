using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Groups
{
    /// <summary>
    /// Aggregates stats from group members into GroupMetrics.
    /// Phase 1: Basic aggregation (member counts, average health/morale, threat).
    /// Phase 2: Extended with detailed stats, cohesion calculations, etc.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GroupDecisionSystemGroup))]
    public partial struct GroupMetricsSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
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

            _transformLookup.Update(ref state);

            // Update metrics for all groups
            foreach (var (members, metrics, groupConfig, groupAggregate, entity) in
                SystemAPI.Query<DynamicBuffer<GroupMember>, RefRW<GroupMetrics>, RefRO<GroupConfig>, RefRO<GroupAggregate>>()
                .WithEntityAccess())
            {
                // Check if we should recompute (respect aggregation interval)
                var ticksSinceCompute = timeState.Tick - metrics.ValueRO.LastComputedTick;
                if (ticksSinceCompute < groupConfig.ValueRO.AggregationInterval)
                {
                    continue;
                }

                if (members.Length == 0)
                {
                    // Empty group - zero out metrics
                    metrics.ValueRW = new GroupMetrics
                    {
                        MemberCount = 0,
                        ActiveMemberCount = 0,
                        ThreatLevel = 0,
                        AverageHealth = 0f,
                        AverageMorale = 0f,
                        LastComputedTick = timeState.Tick
                    };
                    continue;
                }

                // Aggregate member stats
                int activeCount = 0;
                float totalHealth = 0f;
                byte maxThreat = 0;

                // Clear member type counts
                metrics.ValueRW.MemberCountType0 = 0;
                metrics.ValueRW.MemberCountType1 = 0;
                metrics.ValueRW.MemberCountType2 = 0;
                metrics.ValueRW.MemberCountType3 = 0;
                metrics.ValueRW.MemberCountType4 = 0;
                metrics.ValueRW.MemberCountType5 = 0;
                metrics.ValueRW.MemberCountType6 = 0;
                metrics.ValueRW.MemberCountType7 = 0;
                metrics.ValueRW.MemberCountType8 = 0;
                metrics.ValueRW.MemberCountType9 = 0;
                metrics.ValueRW.MemberCountType10 = 0;
                metrics.ValueRW.MemberCountType11 = 0;
                metrics.ValueRW.MemberCountType12 = 0;
                metrics.ValueRW.MemberCountType13 = 0;
                metrics.ValueRW.MemberCountType14 = 0;
                metrics.ValueRW.MemberCountType15 = 0;

                // Clear resource counts
                metrics.ValueRW.ResourceCount0 = 0f;
                metrics.ValueRW.ResourceCount1 = 0f;
                metrics.ValueRW.ResourceCount2 = 0f;
                metrics.ValueRW.ResourceCount3 = 0f;
                metrics.ValueRW.ResourceCount4 = 0f;
                metrics.ValueRW.ResourceCount5 = 0f;
                metrics.ValueRW.ResourceCount6 = 0f;
                metrics.ValueRW.ResourceCount7 = 0f;

                foreach (var member in members)
                {
                    if (!SystemAPI.Exists(member.MemberEntity))
                    {
                        continue;
                    }

                    // Count active members
                    if ((member.Flags & GroupMemberFlags.Active) != 0)
                    {
                        activeCount++;
                    }

                    // Aggregate health (if available)
                    if (SystemAPI.HasComponent<Health>(member.MemberEntity))
                    {
                        var health = SystemAPI.GetComponent<Health>(member.MemberEntity);
                        totalHealth += health.Current / math.max(health.Max, 1f); // Normalize to 0-1
                    }

                    // Aggregate morale (if available - Phase 1: simple check)
                    // TODO Phase 2: Add morale component lookup

                    // Track threat level (if available)
                    // TODO Phase 2: Get threat from member's combat state or threat component

                    // Count by member type (Phase 1: simple role-based)
                    var roleIndex = (int)member.Role;
                    switch (roleIndex)
                    {
                        case 0: metrics.ValueRW.MemberCountType0++; break;
                        case 1: metrics.ValueRW.MemberCountType1++; break;
                        case 2: metrics.ValueRW.MemberCountType2++; break;
                        case 3: metrics.ValueRW.MemberCountType3++; break;
                        case 4: metrics.ValueRW.MemberCountType4++; break;
                        case 5: metrics.ValueRW.MemberCountType5++; break;
                        case 6: metrics.ValueRW.MemberCountType6++; break;
                        case 7: metrics.ValueRW.MemberCountType7++; break;
                        case 8: metrics.ValueRW.MemberCountType8++; break;
                        case 9: metrics.ValueRW.MemberCountType9++; break;
                        case 10: metrics.ValueRW.MemberCountType10++; break;
                        case 11: metrics.ValueRW.MemberCountType11++; break;
                        case 12: metrics.ValueRW.MemberCountType12++; break;
                        case 13: metrics.ValueRW.MemberCountType13++; break;
                        case 14: metrics.ValueRW.MemberCountType14++; break;
                        case 15: metrics.ValueRW.MemberCountType15++; break;
                    }
                }

                // Compute averages
                var memberCount = members.Length;
                var avgHealth = memberCount > 0 ? totalHealth / memberCount : 0f;
                var avgMorale = 0f; // TODO Phase 2: Compute from actual morale data

                // Update metrics
                metrics.ValueRW.MemberCount = memberCount;
                metrics.ValueRW.ActiveMemberCount = activeCount;
                metrics.ValueRW.AverageHealth = avgHealth;
                metrics.ValueRW.AverageMorale = avgMorale;
                metrics.ValueRW.ThreatLevel = maxThreat;
                metrics.ValueRW.LastComputedTick = timeState.Tick;

                // Use existing GroupAggregate for cohesion (if available)
                // Phase 1: GroupAggregate cohesion is already computed, we can reference it
                // Note: RefRO always exists if component is present, no need to check IsCreated
            }
        }
    }
}

