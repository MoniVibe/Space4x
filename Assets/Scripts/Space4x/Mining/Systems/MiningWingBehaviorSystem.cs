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

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _miningProfileLookup = state.GetComponentLookup<MiningPatternProfile>(true);
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

            _miningProfileLookup.Update(ref state);
            _craftFrameLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _personalityLookup.Update(ref state);

            var time = SystemAPI.Time;

            var job = new ProcessMiningWingsJob
            {
                CurrentTick = (uint)time.ElapsedTime, // use elapsed time seconds cast to uint ticks for now
                MiningProfileLookup = _miningProfileLookup,
                CraftFrameLookup = _craftFrameLookup,
                IntentLookup = _intentLookup,
                FocusLookup = _focusLookup,
                PersonalityLookup = _personalityLookup
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

            void Execute(
                in GroupTag groupTag,
                in GroupMeta groupMeta,
                in GroupStanceState groupStance,
                DynamicBuffer<GroupMember> members)
            {
                // Only process MiningWing groups
                if (groupMeta.Kind != GroupKind.MiningWing)
                {
                    return;
                }

                // Group chooses which deposit(s) to target
                // Would select deposit targets here based on group stance and available deposits

                // Process individual mining craft
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

                    // Get craft frame and mining profile
                    if (!CraftFrameLookup.HasComponent(member.Member))
                    {
                        continue;
                    }

                    var frameRef = CraftFrameLookup[member.Member];
                    // Would look up MiningPatternProfile by FrameId here
                    // For now, use default values

                    float optimalRange = 5f; // Default
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
                    if (FocusLookup.HasComponent(member.Member) && PersonalityLookup.HasComponent(member.Member))
                    {
                        var focus = FocusLookup[member.Member];
                        var personality = PersonalityLookup[member.Member];

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

