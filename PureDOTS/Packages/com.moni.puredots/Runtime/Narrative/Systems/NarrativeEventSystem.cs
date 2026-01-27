using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Narrative;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Narrative
{
    /// <summary>
    /// Evaluates and fires one-shot narrative events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct NarrativeEventSystem : ISystem
    {
        private uint _lastEventCheckTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NarrativeRegistrySingleton>();
            state.RequireForUpdate<TimeState>();
            _lastEventCheckTick = 0;
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

            // Check events periodically (every 10 ticks)
            if (timeState.Tick - _lastEventCheckTick < 10)
            {
                return;
            }

            _lastEventCheckTick = timeState.Tick;

            var registry = SystemAPI.GetSingleton<NarrativeRegistrySingleton>();
            
            if (!registry.EventRegistry.IsCreated)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<NarrativeSignalBufferElement>(out var signalEntity))
            {
                return;
            }

            var signalBuffer = state.EntityManager.GetBuffer<NarrativeSignalBufferElement>(signalEntity);

            if (!SystemAPI.TryGetSingletonEntity<NarrativeEffectRequest>(out var effectEntity))
            {
                return;
            }

            var effectBuffer = state.EntityManager.GetBuffer<NarrativeEffectRequest>(effectEntity);

            ref var eventRegistry = ref registry.EventRegistry.Value;

            // Evaluate each event
            for (int i = 0; i < eventRegistry.Events.Length; i++)
            {
                ref var eventDef = ref eventRegistry.Events[i];

                // Check conditions
                bool conditionsMet = true;

                for (int j = 0; j < eventDef.Conditions.Length; j++)
                {
                    ref var condition = ref eventDef.Conditions[j];
                    
                    // Simple condition evaluation (stub - expand based on condition types)
                    if (condition.ConditionType == NarrativeRegistryBuilder.ConditionTypeRandomRoll)
                    {
                        // Random roll: ParamA = chance out of ParamB
                        var rng = new Unity.Mathematics.Random((uint)(i + j + timeState.Tick));
                        float roll = rng.NextFloat();
                        float threshold = (float)condition.ParamA / (float)condition.ParamB;
                        if (roll > threshold)
                        {
                            conditionsMet = false;
                            break;
                        }
                    }
                    // Add more condition types as needed
                }

                if (conditionsMet)
                {
                    // Fire event: emit signal
                    signalBuffer.Add(new NarrativeSignalBufferElement
                    {
                        SignalType = 2, // EventFired
                        Id = eventDef.Id,
                        Target = Entity.Null,
                        PayloadA = eventDef.TitleKey,
                        PayloadB = eventDef.BodyKey
                    });

                    // Apply effects
                    for (int j = 0; j < eventDef.Effects.Length; j++)
                    {
                        ref var effect = ref eventDef.Effects[j];
                        effectBuffer.Add(new NarrativeEffectRequest
                        {
                            EffectType = effect.EffectType,
                            ParamA = effect.ParamA,
                            ParamB = effect.ParamB,
                            SituationEntity = Entity.Null
                        });
                    }
                }
            }
        }
    }
}

