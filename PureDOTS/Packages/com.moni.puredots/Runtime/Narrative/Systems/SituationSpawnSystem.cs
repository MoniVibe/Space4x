using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Narrative;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Narrative
{
    /// <summary>
    /// Spawns situation instances from spawn requests.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SituationSpawnSystem : ISystem
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
            if (timeState.IsPaused)
            {
                return;
            }

            // Get inbox singleton with spawn requests
            if (!SystemAPI.TryGetSingletonEntity<SituationSpawnRequest>(out var inboxEntity))
            {
                return;
            }

            var spawnRequests = state.EntityManager.GetBuffer<SituationSpawnRequest>(inboxEntity);
            
            if (spawnRequests.Length == 0)
            {
                return;
            }

            var registry = SystemAPI.GetSingleton<NarrativeRegistrySingleton>();
            
            if (!SystemAPI.TryGetSingletonEntity<NarrativeSignalBufferElement>(out var signalEntity))
            {
                return;
            }
            
            var signalBuffer = state.EntityManager.GetBuffer<NarrativeSignalBufferElement>(signalEntity);

            var worldTime = timeState.ElapsedTime;

            // Process each spawn request
            for (int i = spawnRequests.Length - 1; i >= 0; i--)
            {
                var request = spawnRequests[i];
                
                // Find archetype in registry
                if (registry.SituationRegistry.IsCreated)
                {
                    ref var situationRegistry = ref registry.SituationRegistry.Value;
                    for (int j = 0; j < situationRegistry.Archetypes.Length; j++)
                    {
                        ref var archetype = ref situationRegistry.Archetypes[j];
                        if (archetype.SituationId.Value == request.SituationId.Value)
                        {
                            // Create situation instance entity
                            var situationEntity = state.EntityManager.CreateEntity();
                            
                            var situationInstance = new SituationInstance
                            {
                                SituationId = request.SituationId,
                                Phase = SituationPhase.Intro,
                                StepIndex = 0,
                                NextEvaluationTime = worldTime,
                                StartedAt = worldTime,
                                LastStepChangeAt = worldTime,
                                IsBackground = 0
                            };
                            state.EntityManager.AddComponentData(situationEntity, situationInstance);

                            var situationContext = new SituationContext
                            {
                                Location = request.Location,
                                OwningFaction = request.Faction,
                                Tags = request.Tags
                            };
                            state.EntityManager.AddComponentData(situationEntity, situationContext);

                            // Add participant buffer (empty for now, can be filled by game-side)
                            state.EntityManager.AddBuffer<SituationParticipant>(situationEntity);
                            state.EntityManager.AddBuffer<SituationFlag>(situationEntity);

                            // Emit signal
                            signalBuffer.Add(new NarrativeSignalBufferElement
                            {
                                SignalType = 0, // SituationStarted
                                Id = request.SituationId,
                                Target = request.Location,
                                PayloadA = 0,
                                PayloadB = 0
                            });

                            break;
                        }
                    }
                }

                // Remove processed request
                spawnRequests.RemoveAt(i);
            }
        }
    }
}

