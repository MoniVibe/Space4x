using PureDOTS.Runtime.Focus;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Vehicles;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Space4X.Demo;
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
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GroupIntentResolutionSystem))]
    public partial struct StrikeWingBehaviorSystem : ISystem
    {
        ComponentLookup<AttackRunProfile> _attackProfileLookup;
        ComponentLookup<CraftFrameRef> _craftFrameLookup;
        ComponentLookup<IndividualCombatIntent> _intentLookup;
        ComponentLookup<FocusState> _focusLookup;
        ComponentLookup<PersonalityAxes> _personalityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _attackProfileLookup = state.GetComponentLookup<AttackRunProfile>(true);
            _craftFrameLookup = state.GetComponentLookup<CraftFrameRef>(true);
            _intentLookup = state.GetComponentLookup<IndividualCombatIntent>(true);
            _focusLookup = state.GetComponentLookup<FocusState>(true);
            _personalityLookup = state.GetComponentLookup<PersonalityAxes>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<DemoScenarioState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<DemoScenarioState>(out var scenario) ||
                !scenario.EnableSpace4x ||
                !scenario.IsInitialized)
            {
                return;
            }

            _attackProfileLookup.Update(ref state);
            _craftFrameLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _personalityLookup.Update(ref state);

            var time = SystemAPI.Time;

            var job = new ProcessStrikeWingsJob
            {
                CurrentTick = (uint)time.ElapsedTime, // use elapsed time seconds cast to uint ticks for now
                AttackProfileLookup = _attackProfileLookup,
                CraftFrameLookup = _craftFrameLookup,
                IntentLookup = _intentLookup,
                FocusLookup = _focusLookup,
                PersonalityLookup = _personalityLookup
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

            void Execute(
                in GroupTag groupTag,
                in GroupMeta groupMeta,
                in GroupStanceState groupStance,
                DynamicBuffer<GroupMember> members)
            {
                // Only process StrikeWing groups
                if (groupMeta.Kind != GroupKind.StrikeWing)
                {
                    return;
                }

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
                    if (member.Member == Entity.Null)
                    {
                        continue;
                    }

                    // Check member intent (may override group orders)
                    if (IntentLookup.HasComponent(member.Member))
                    {
                        var intent = IntentLookup[member.Member];
                        if (intent.Intent == IndividualTacticalIntent.Flee ||
                            intent.Intent == IndividualTacticalIntent.Desert ||
                            intent.Intent == IndividualTacticalIntent.Mutiny)
                        {
                            continue; // Member not following orders
                        }
                    }

                    // Get craft frame and attack profile
                    if (!CraftFrameLookup.HasComponent(member.Member))
                    {
                        continue;
                    }

                    // Would look up AttackRunProfile by FrameId here
                    // For now, use default values

                    // Weight by pilot Focus & PersonalityAxes
                    float breakDistance = 10f; // Default

                    if (FocusLookup.HasComponent(member.Member) && PersonalityLookup.HasComponent(member.Member))
                    {
                        var focus = FocusLookup[member.Member];
                        var personality = PersonalityLookup[member.Member];

                        // Bold + high Focus → commit deeper, dodge aggressively
                        if (personality.Boldness > 0.5f && focus.Current > focus.SoftThreshold)
                        {
                            breakDistance *= 0.7f; // Closer break-off
                        }

                        // Craven + low Focus → earlier break, may loiter
                        if (personality.Boldness < -0.3f || focus.Current < focus.SoftThreshold)
                        {
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

