using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Focus;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Groups
{
    /// <summary>
    /// Surrender decision system for individuals in groups.
    /// On individual Morale check fail: if group doesn't route but individual does,
    /// intent Flee or Surrender based on Peaceful/Craven vs Vengeful, Intelligence/Wisdom.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GroupMoraleSystem))]
    public partial struct SurrenderDecisionSystem : ISystem
    {
        ComponentLookup<MoraleState> _moraleLookup;
        ComponentLookup<GroupMoraleState> _groupMoraleLookup;
        ComponentLookup<PersonalityAxes> _personalityLookup;
        ComponentLookup<AlignmentTriplet> _alignmentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _moraleLookup = state.GetComponentLookup<MoraleState>(false);
            _groupMoraleLookup = state.GetComponentLookup<GroupMoraleState>(true);
            _personalityLookup = state.GetComponentLookup<PersonalityAxes>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _moraleLookup.Update(ref state);
            _groupMoraleLookup.Update(ref state);
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            
            _personalityLookup.Update(ref state);
            _alignmentLookup.Update(ref state);

            var job = new ProcessSurrenderDecisionsJob
            {
                CurrentTick = timeState.Tick,
                MoraleLookup = _moraleLookup,
                GroupMoraleLookup = _groupMoraleLookup,
                PersonalityLookup = _personalityLookup,
                AlignmentLookup = _alignmentLookup
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ProcessSurrenderDecisionsJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<MoraleState> MoraleLookup;
            [ReadOnly] public ComponentLookup<GroupMoraleState> GroupMoraleLookup;
            [ReadOnly] public ComponentLookup<PersonalityAxes> PersonalityLookup;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;

            void Execute(
                ref IndividualCombatIntent intent,
                Entity entity,
                in GroupBehaviorParams behaviorParams)
            {
                // Skip if already decided to flee/desert/mutiny
                if (intent.Intent == IndividualTacticalIntent.Flee ||
                    intent.Intent == IndividualTacticalIntent.Desert ||
                    intent.Intent == IndividualTacticalIntent.Mutiny)
                {
                    return;
                }

                // Check individual morale
                if (!MoraleLookup.HasComponent(entity))
                {
                    return;
                }

                var morale = MoraleLookup[entity];

                // Individual morale check fail: morale below threshold
                float surrenderThreshold = -0.5f; // Very low morale
                if (morale.Current > surrenderThreshold)
                {
                    return; // Morale not low enough
                }

                // Check if group is routing
                // Would need to find group entity from member
                // For now, assume group is not routing if individual morale fails

                // Determine surrender vs flee based on Peaceful/Craven vs Vengeful
                bool shouldSurrender = false;

                if (PersonalityLookup.HasComponent(entity) && AlignmentLookup.HasComponent(entity))
                {
                    var personality = PersonalityLookup[entity];
                    var alignment = AlignmentLookup[entity];

                    // Peaceful/Craven + high Intelligence/Wisdom → surrender early
                    // Would need Intelligence/Wisdom stats - for now use alignment Moral axis (Good = peaceful)
                    bool isPeaceful = alignment.Moral > 0.3f; // Good alignment
                    bool isCraven = personality.Boldness < -0.3f; // Low boldness
                    bool isVengeful = personality.Vengefulness > 0.3f; // High vengefulness

                    // Smart enough to see odds (would need Intelligence stat)
                    // For now, assume moderate intelligence
                    bool isSmart = true; // Placeholder

                    if ((isPeaceful || isCraven) && isSmart)
                    {
                        // Surrender if odds overwhelming
                        shouldSurrender = true;
                    }
                    else if (isVengeful)
                    {
                        // Vengeful individuals less likely to surrender
                        shouldSurrender = false;
                    }
                }

                // Set intent
                if (shouldSurrender)
                {
                    // Surrender intent (would need new intent type or use Flee with surrender flag)
                    // For now, use CautiousHold as surrender proxy
                    intent.Intent = IndividualTacticalIntent.CautiousHold;
                    intent.TargetOverride = Entity.Null;
                }
                else
                {
                    // Flee instead of surrendering
                    intent.Intent = IndividualTacticalIntent.Flee;
                    intent.TargetOverride = Entity.Null;
                }
            }
        }
    }
}

