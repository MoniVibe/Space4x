using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Narrative;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Narrative
{
    /// <summary>
    /// Drives situation state machine transitions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SituationUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NarrativeRegistrySingleton>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var registry = SystemAPI.GetSingleton<NarrativeRegistrySingleton>();
            var worldTime = (double)timeState.ElapsedTime;

            // Get inbox singleton for choices and world facts
            if (!SystemAPI.TryGetSingletonEntity<SituationChoice>(out var inboxEntity))
            {
                return;
            }

            var choices = state.EntityManager.GetBuffer<SituationChoice>(inboxEntity);
            var worldFacts = state.EntityManager.GetBuffer<WorldFactEvent>(inboxEntity);

            // Get signal buffer
            if (!SystemAPI.TryGetSingletonEntity<NarrativeSignalBufferElement>(out var signalEntity))
            {
                return;
            }

            var signalBuffer = state.EntityManager.GetBuffer<NarrativeSignalBufferElement>(signalEntity);

            // Get effect request buffer
            if (!SystemAPI.TryGetSingletonEntity<NarrativeEffectRequest>(out var effectEntity))
            {
                return;
            }

            var effectBuffer = state.EntityManager.GetBuffer<NarrativeEffectRequest>(effectEntity);

            // Query all situation instances
            foreach (var (instance, context, entity) in SystemAPI.Query<RefRW<SituationInstance>, RefRO<SituationContext>>().WithEntityAccess())
            {
                // Skip if not due for evaluation
                if (worldTime < instance.ValueRO.NextEvaluationTime)
                {
                    continue;
                }

                // Load archetype from registry
                if (!registry.SituationRegistry.IsCreated)
                {
                    continue;
                }

                ref var situationRegistry = ref registry.SituationRegistry.Value;
                int archetypeIndex = -1;
                
                for (int i = 0; i < situationRegistry.Archetypes.Length; i++)
                {
                    ref var arch = ref situationRegistry.Archetypes[i];
                    if (arch.SituationId.Value == instance.ValueRO.SituationId.Value)
                    {
                        archetypeIndex = i;
                        break;
                    }
                }

                if (archetypeIndex < 0)
                {
                    continue;
                }

                ref var archetype = ref situationRegistry.Archetypes[archetypeIndex];

                // Get current step
                var currentStepIndex = instance.ValueRO.StepIndex;
                int stepIndex = -1;

                for (int i = 0; i < archetype.Steps.Length; i++)
                {
                    ref var stepDef = ref archetype.Steps[i];
                    if (stepDef.StepIndex == currentStepIndex)
                    {
                        stepIndex = i;
                        break;
                    }
                }

                if (stepIndex < 0)
                {
                    continue;
                }

                ref var currentStep = ref archetype.Steps[stepIndex];

                // Evaluate step timeout
                var stepDuration = worldTime - instance.ValueRO.LastStepChangeAt;
                var minDuration = (double)currentStep.MinDuration;
                var maxDuration = (double)currentStep.MaxDuration;

                // Find valid transitions from current step
                var validTransitions = new NativeList<int>(Allocator.Temp);
                
                for (int i = 0; i < archetype.Transitions.Length; i++)
                {
                    ref var transition = ref archetype.Transitions[i];
                    
                    if (transition.FromStepIndex != currentStepIndex)
                    {
                        continue;
                    }

                    // Check if conditions are met
                    bool conditionsMet = true;

                    // Check time-based conditions
                    if (currentStep.Kind == SituationStepKind.TimedTick || currentStep.Kind == SituationStepKind.AutoAdvance)
                    {
                        if (stepDuration < minDuration)
                        {
                            conditionsMet = false;
                        }
                    }

                    // Check transition conditions
                    for (int j = 0; j < transition.Conditions.Length; j++)
                    {
                        ref var condition = ref transition.Conditions[j];
                        
                        // Simple condition evaluation (stub - expand based on condition types)
                        if (condition.ConditionType == NarrativeRegistryBuilder.ConditionTypePlayerChoiceEquals)
                        {
                            // Check if there's a matching choice
                            bool foundChoice = false;
                            for (int k = 0; k < choices.Length; k++)
                            {
                                if (choices[k].SituationEntity == entity && choices[k].OptionIndex == condition.ParamA)
                                {
                                    foundChoice = true;
                                    break;
                                }
                            }
                            
                            if (!foundChoice)
                            {
                                conditionsMet = false;
                                break;
                            }
                        }
                        else if (condition.ConditionType == NarrativeRegistryBuilder.ConditionTypeTimeElapsed)
                        {
                            if (stepDuration < minDuration)
                            {
                                conditionsMet = false;
                                break;
                            }
                        }
                        // Add more condition types as needed
                    }

                    if (conditionsMet)
                    {
                        validTransitions.Add(i);
                    }
                }

                // Select transition (weighted random if multiple)
                if (validTransitions.Length > 0)
                {
                    int selectedTransitionIndex = validTransitions[0];
                    
                    if (validTransitions.Length > 1)
                    {
                        // Weighted random selection (simplified - use first for now)
                        float totalWeight = 0f;
                        for (int i = 0; i < validTransitions.Length; i++)
                        {
                            ref var trans = ref archetype.Transitions[validTransitions[i]];
                            totalWeight += trans.Weight;
                        }

                        var rng = new Unity.Mathematics.Random((uint)(entity.Index + timeState.Tick));
                        float random = rng.NextFloat() * totalWeight;
                        float accumulated = 0f;
                        
                        for (int i = 0; i < validTransitions.Length; i++)
                        {
                            ref var trans = ref archetype.Transitions[validTransitions[i]];
                            accumulated += trans.Weight;
                            if (random <= accumulated)
                            {
                                selectedTransitionIndex = validTransitions[i];
                                break;
                            }
                        }
                    }

                    ref var selectedTransition = ref archetype.Transitions[selectedTransitionIndex];

                    // Apply transition effects
                    for (int i = 0; i < selectedTransition.Effects.Length; i++)
                    {
                        ref var effect = ref selectedTransition.Effects[i];
                        effectBuffer.Add(new NarrativeEffectRequest
                        {
                            EffectType = effect.EffectType,
                            ParamA = effect.ParamA,
                            ParamB = effect.ParamB,
                            SituationEntity = entity
                        });
                    }

                    // Update step
                    var newStepIndex = selectedTransition.ToStepIndex;
                    var newPhase = instance.ValueRO.Phase;

                    if (newStepIndex >= archetype.Steps.Length)
                    {
                        newPhase = SituationPhase.Finished;
                    }
                    else if (newStepIndex == currentStepIndex && newPhase != SituationPhase.Finished)
                    {
                        newPhase = SituationPhase.Finished; // Self-loop marks finished
                    }
                    else if (newStepIndex == 0)
                    {
                        newPhase = SituationPhase.Intro;
                    }
                    else
                    {
                        newPhase = SituationPhase.Running;
                    }

                    instance.ValueRW.StepIndex = newStepIndex;
                    instance.ValueRW.Phase = newPhase;
                    instance.ValueRW.LastStepChangeAt = worldTime;

                    // Calculate next evaluation time
                    if (newStepIndex < archetype.Steps.Length)
                    {
                        ref var newStep = ref archetype.Steps[newStepIndex];
                        var nextMinDuration = (double)newStep.MinDuration;
                        instance.ValueRW.NextEvaluationTime = worldTime + nextMinDuration;
                    }
                    else
                    {
                        instance.ValueRW.NextEvaluationTime = worldTime + 1.0;
                    }

                    // Emit signal for step change
                    signalBuffer.Add(new NarrativeSignalBufferElement
                    {
                        SignalType = 1, // StepEntered
                        Id = instance.ValueRO.SituationId,
                        Target = context.ValueRO.Location,
                        PayloadA = newStepIndex,
                        PayloadB = 0
                    });
                }

                validTransitions.Dispose();
            }

            // Clear processed choices (simple - remove all for now)
            choices.Clear();
        }
    }
}

