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

namespace Space4X.Mining
{
    /// <summary>
    /// Mining wing behavior system for Space4x.
    /// For mining wing (GroupKind = MiningWing): group chooses deposit targets,
    /// individual craft follow MiningPatternProfile, risk-based retreat, personality-driven bailout.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(GroupIntentResolutionSystem))]
    public partial struct MiningWingBehaviorSystem : ISystem
    {
        ComponentLookup<MiningPatternProfile> _miningProfileLookup;
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
            
            _miningProfileLookup = state.GetComponentLookup<MiningPatternProfile>(true);
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
            _miningProfileLookup.Update(ref state);
            _craftFrameLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _personalityLookup.Update(ref state);
            _groupStanceLookup.Update(ref state);
            _groupMetaLookup.Update(ref state);
            _groupMemberLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();

            var job = new ProcessMiningWingsJob
            {
                CurrentTick = timeState.Tick,
                MiningProfileLookup = _miningProfileLookup,
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
        partial struct ProcessMiningWingsJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<MiningPatternProfile> MiningProfileLookup;
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
                // Only process MiningWing groups
                if (groupMeta.Kind != GroupKind.MiningWing)
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

                // Group chooses which deposit(s) to target
                // Would select deposit targets here based on group stance and available deposits

                // Process individual mining craft
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

                    // Get craft frame and mining profile
                    if (!CraftFrameLookup.HasComponent(member.MemberEntity))
                    {
                        continue;
                    }

                    var frameRef = CraftFrameLookup[member.MemberEntity];
                    // Would look up MiningPatternProfile by FrameId here
                    // For now, use default values

                    float retreatHullThreshold = 0.3f; // Default 30% hull

                    // Follow group's stance on risk
                    if (groupStance.Stance == GroupStance.Attack)
                    {
                        // Aggressive → mine longer in hostile conditions
                        retreatHullThreshold *= 0.7f; // Lower threshold = mine longer
                    }
                    else if (groupStance.Stance == GroupStance.Retreat || groupStance.Stance == GroupStance.Hold)
                    {
                        // Cautious → retreat early if hazards rise
                        retreatHullThreshold *= 1.5f; // Higher threshold = retreat earlier
                    }

                    // Personality/Fear: Craven/low Focus craft bail earlier
                    if (FocusLookup.HasComponent(member.MemberEntity) && PersonalityLookup.HasComponent(member.MemberEntity))
                    {
                        var focus = FocusLookup[member.MemberEntity];
                        var personality = PersonalityLookup[member.MemberEntity];

                        // Craven or low Focus → bail earlier
                        if (personality.Boldness < -0.3f || focus.Current < focus.SoftThreshold)
                        {
                            retreatHullThreshold *= 1.3f; // Retreat even earlier
                        }
                    }

                    // Approach deposit based on MiningPatternProfile
                    // Would set approach target and mining parameters here
                }
            }
        }
    }
}


