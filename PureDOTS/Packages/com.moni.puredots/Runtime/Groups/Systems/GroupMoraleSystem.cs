using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Groups
{
    /// <summary>
    /// Aggregates member morale into GroupMoraleState.
    /// Calculates CasualtyRatio from member deaths, sets Routing flag when thresholds exceeded.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GroupMoraleSystem : ISystem
    {
        ComponentLookup<MoraleState> _moraleLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _moraleLookup = state.GetComponentLookup<MoraleState>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            
            _moraleLookup.Update(ref state);

            var job = new UpdateGroupMoraleJob
            {
                CurrentTick = timeState.Tick,
                MoraleLookup = _moraleLookup
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct UpdateGroupMoraleJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<MoraleState> MoraleLookup;

            void Execute(
                ref GroupMoraleState groupMorale,
                in GroupMeta groupMeta,
                DynamicBuffer<GroupMember> members)
            {
                if (members.Length == 0)
                {
                    groupMorale.AverageMorale = 0f;
                    groupMorale.CasualtyRatio = 1f;
                    groupMorale.Routing = 1;
                    return;
                }

                // Aggregate member morale
                float totalMorale = 0f;
                int aliveCount = 0;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (member.MemberEntity == Entity.Null)
                    {
                        continue; // Dead member
                    }

                    // Check if member has MoraleState component
                    if (MoraleLookup.HasComponent(member.MemberEntity))
                    {
                        var memberMorale = MoraleLookup[member.MemberEntity];
                        totalMorale += memberMorale.Current;
                        aliveCount++;
                    }
                }

                // Calculate average morale
                if (aliveCount > 0)
                {
                    groupMorale.AverageMorale = totalMorale / aliveCount;
                }
                else
                {
                    groupMorale.AverageMorale = -1f; // All dead
                }

                // Calculate casualty ratio
                int totalMembers = groupMeta.MaxSize > 0 ? groupMeta.MaxSize : members.Length;
                int deadCount = totalMembers - aliveCount;
                groupMorale.CasualtyRatio = totalMembers > 0 ? (float)deadCount / totalMembers : 0f;

                // Set Routing flag when thresholds exceeded
                float routingThreshold = 0.3f; // 30% casualties or morale < -0.5
                if (groupMorale.CasualtyRatio > routingThreshold || groupMorale.AverageMorale < -0.5f)
                {
                    groupMorale.Routing = 1;
                }
                else if (groupMorale.CasualtyRatio < routingThreshold * 0.5f && groupMorale.AverageMorale > 0f)
                {
                    // Recovery: stop routing if casualties drop and morale improves
                    groupMorale.Routing = 0;
                }
            }
        }
    }
}

