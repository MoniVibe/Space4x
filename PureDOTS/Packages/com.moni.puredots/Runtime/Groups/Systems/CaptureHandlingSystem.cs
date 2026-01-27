using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Groups
{
    /// <summary>
    /// Capture handling system - applies outcome based on captor's alignment.
    /// Release, ransom, enslave, execute based on captor alignment & personality.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SurrenderDecisionSystem))]
    public partial struct CaptureHandlingSystem : ISystem
    {
        ComponentLookup<AlignmentTriplet> _alignmentLookup;
        ComponentLookup<PersonalityAxes> _personalityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _personalityLookup = state.GetComponentLookup<PersonalityAxes>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            
            _alignmentLookup.Update(ref state);
            _personalityLookup.Update(ref state);

            var job = new ProcessCapturesJob
            {
                CurrentTick = timeState.Tick,
                AlignmentLookup = _alignmentLookup,
                PersonalityLookup = _personalityLookup
            };
            job.ScheduleParallel();
        }

        /// <summary>
        /// Capture outcome based on captor alignment.
        /// </summary>
        public enum CaptureOutcome : byte
        {
            Release,    // Set free
            Ransom,     // Demand payment
            Enslave,    // Enslave prisoner
            Execute     // Execute prisoner
        }

        [BurstCompile]
        partial struct ProcessCapturesJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;
            [ReadOnly] public ComponentLookup<PersonalityAxes> PersonalityLookup;

            void Execute(
                in IndividualCombatIntent intent,
                Entity entity)
            {
                // Only process if prisoner has surrendered (CautiousHold intent)
                if (intent.Intent != IndividualTacticalIntent.CautiousHold)
                {
                    return;
                }

                // TODO: This system needs to be refactored to work with group member buffers
                // GroupMember is a buffer element, not a component, so it can't be used as a parameter
                // For now, skip processing until the system is redesigned to query group entities
                // and iterate through their member buffers
                return;

                // Determine outcome based on captor's alignment
                // CaptureOutcome outcome = DetermineCaptureOutcome(captorEntity);

                // Apply outcome
                // Would set prisoner state, transfer ownership, etc. here
                // For now, outcome is determined but not applied (would need prisoner state component)
            }

            CaptureOutcome DetermineCaptureOutcome(Entity captorEntity)
            {
                if (!AlignmentLookup.HasComponent(captorEntity))
                {
                    return CaptureOutcome.Ransom; // Default neutral outcome
                }

                var alignment = AlignmentLookup[captorEntity];

                // Good alignment → Release or Ransom
                if (alignment.Moral > 0.5f)
                {
                    // Check if captor is also Peaceful
                    if (PersonalityLookup.HasComponent(captorEntity))
                    {
                        var personality = PersonalityLookup[captorEntity];
                        // Would check for Peaceful outlook here
                        // For now, use alignment Moral axis
                        if (alignment.Moral > 0.7f)
                        {
                            return CaptureOutcome.Release; // Very good = release
                        }
                    }
                    return CaptureOutcome.Ransom; // Good but pragmatic = ransom
                }

                // Evil alignment → Enslave or Execute
                if (alignment.Moral < -0.5f)
                {
                    // Corrupt evil → Enslave (profitable)
                    if (alignment.Purity < -0.3f)
                    {
                        return CaptureOutcome.Enslave;
                    }
                    // Pure evil → Execute (ideological)
                    return CaptureOutcome.Execute;
                }

                // Neutral alignment → Ransom (pragmatic)
                return CaptureOutcome.Ransom;
            }
        }
    }
}

