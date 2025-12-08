using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Focus;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Vehicles;
using PureDOTS.Runtime.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.StrikeCraft
{
    /// <summary>
    /// Strike wing behavior system for Space4x.
    /// Per wing (GroupKind = StrikeWing): decides attack pattern from GroupStance,
    /// uses AttackRunProfile, weights by pilot Focus & PersonalityAxes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.CoreSingletonBootstrapSystem))]
    [UpdateAfter(typeof(GroupIntentResolutionSystem))]
    public partial struct StrikeWingBehaviorSystem : ISystem
    {
        ComponentLookup<AttackRunProfile> _attackProfileLookup;
        ComponentLookup<CraftFrameRef> _craftFrameLookup;
        ComponentLookup<IndividualCombatIntent> _intentLookup;
        ComponentLookup<FocusState> _focusLookup;
        ComponentLookup<PersonalityAxes> _personalityLookup;
        ComponentLookup<GroupStanceState> _groupStanceLookup;
        BufferLookup<GroupMember> _groupMemberLookup;
        ComponentLookup<GroupMeta> _groupMetaLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            _attackProfileLookup = state.GetComponentLookup<AttackRunProfile>(true);
            _craftFrameLookup = state.GetComponentLookup<CraftFrameRef>(true);
            _intentLookup = state.GetComponentLookup<IndividualCombatIntent>(true);
            _focusLookup = state.GetComponentLookup<FocusState>(true);
            _personalityLookup = state.GetComponentLookup<PersonalityAxes>(true);
            _groupStanceLookup = state.GetComponentLookup<GroupStanceState>(true);
            _groupMetaLookup = state.GetComponentLookup<GroupMeta>(true);
            _groupMemberLookup = state.GetBufferLookup<GroupMember>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _attackProfileLookup.Update(ref state);
            _craftFrameLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _personalityLookup.Update(ref state);
            _groupStanceLookup.Update(ref state);
            _groupMetaLookup.Update(ref state);
            _groupMemberLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();

            var job = new ProcessStrikeWingsJob
            {
                CurrentTick = timeState.Tick,
                AttackProfileLookup = _attackProfileLookup,
                CraftFrameLookup = _craftFrameLookup,
                IntentLookup = _intentLookup,
                FocusLookup = _focusLookup,
                PersonalityLookup = _personalityLookup,
                GroupStanceLookup = _groupStanceLookup,
                GroupMetaLookup = _groupMetaLookup,
                GroupMemberLookup = _groupMemberLookup
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ProcessStrikeWingsJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<AttackRunProfile> AttackProfileLookup;
            [ReadOnly] public ComponentLookup<CraftFrameRef> CraftFrameLookup;
            [ReadOnly] public ComponentLookup<IndividualCombatIntent> IntentLookup;
            [ReadOnly] public ComponentLookup<FocusState> FocusLookup;
            [ReadOnly] public ComponentLookup<PersonalityAxes> PersonalityLookup;
            [ReadOnly] public ComponentLookup<GroupStanceState> GroupStanceLookup;
            [ReadOnly] public ComponentLookup<GroupMeta> GroupMetaLookup;
            [ReadOnly] public BufferLookup<GroupMember> GroupMemberLookup;

            void Execute(Entity groupEntity)
            {
                if (!GroupMetaLookup.HasComponent(groupEntity))
                {
                    return;
                }

                var groupMeta = GroupMetaLookup[groupEntity];
                // Only process StrikeWing groups
                if (groupMeta.Kind != GroupKind.StrikeWing)
                {
                    return;
                }

                if (!GroupStanceLookup.HasComponent(groupEntity))
                {
                    return;
                }

                var groupStance = GroupStanceLookup[groupEntity];

                if (!GroupMemberLookup.HasBuffer(groupEntity))
                {
                    return;
                }

                var members = GroupMemberLookup[groupEntity];

                // Decide attack pattern based on GroupStance
                switch (groupStance.Stance)
                {
                    case GroupStance.Attack:
                        // Repeated attack runs on primary target
                        ProcessAttackRuns(members, groupStance.PrimaryTarget);
                        break;

                    case GroupStance.Skirmish:
                        // Harass, high disengage priority
                        ProcessSkirmishRuns(members, groupStance.PrimaryTarget);
                        break;

                    case GroupStance.Screen:
                        // Intercept enemy missiles/drones/strike craft
                        ProcessScreenRuns(members);
                        break;

                    default:
                        // Hold, Retreat, IndependentHunt handled elsewhere
                        break;
                }
            }

            void ProcessAttackRuns(DynamicBuffer<GroupMember> members, Entity primaryTarget)
            {
                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (member.MemberEntity == Entity.Null)
                    {
                        continue;
                    }

                    // Check member intent (may override group orders)
                    if (IntentLookup.HasComponent(member.MemberEntity))
                    {
                        var intent = IntentLookup[member.MemberEntity];
                        if (intent.Intent == IndividualTacticalIntent.Flee ||
                            intent.Intent == IndividualTacticalIntent.Desert ||
                            intent.Intent == IndividualTacticalIntent.Mutiny)
                        {
                            continue; // Member not following orders
                        }
                    }

                    // Get craft frame and attack profile
                    if (!CraftFrameLookup.HasComponent(member.MemberEntity))
                    {
                        continue;
                    }

                    var frameRef = CraftFrameLookup[member.MemberEntity];
                    // Would look up AttackRunProfile by FrameId here
                    // For now, use default values

                    // Weight by pilot Focus & PersonalityAxes
                    float approachDistance = 50f; // Default
                    float breakDistance = 10f; // Default
                    float commitDepth = 1.0f; // Default commitment

                    if (FocusLookup.HasComponent(member.MemberEntity) && PersonalityLookup.HasComponent(member.MemberEntity))
                    {
                        var focus = FocusLookup[member.MemberEntity];
                        var personality = PersonalityLookup[member.MemberEntity];

                        // Bold + high Focus → commit deeper, dodge aggressively
                        if (personality.Boldness > 0.5f && focus.Current > focus.SoftThreshold)
                        {
                            commitDepth = 1.5f; // Deeper commitment
                            breakDistance *= 0.7f; // Closer break-off
                        }

                        // Craven + low Focus → earlier break, may loiter
                        if (personality.Boldness < -0.3f || focus.Current < focus.SoftThreshold)
                        {
                            commitDepth = 0.6f; // Shallow commitment
                            breakDistance *= 1.5f; // Earlier break-off
                        }
                    }

                    // On run completion: Auto-RTB if ammo/hull below threshold
                    // Would check ammo/hull state here and set RTB flag
                }
            }

            void ProcessSkirmishRuns(DynamicBuffer<GroupMember> members, Entity primaryTarget)
            {
                // Similar to attack runs but with higher disengage priority
                // Shorter approach, earlier break-off
                ProcessAttackRuns(members, primaryTarget);
            }

            void ProcessScreenRuns(DynamicBuffer<GroupMember> members)
            {
                // Intercept enemy missiles/drones/strike craft
                // Would set intercept targets here
            }
        }
    }
}

