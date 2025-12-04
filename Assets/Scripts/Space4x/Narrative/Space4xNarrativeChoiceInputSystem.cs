using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Narrative;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4x.Narrative
{
    /// <summary>
    /// Debug input handler for writing situation choices.
    /// Press 'N' key to choose option 0 for the first active situation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4xNarrativeChoiceInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            // Check for 'N' key press
            if (!Input.GetKeyDown(KeyCode.N))
            {
                return;
            }

            // Find first active situation
            Entity? firstSituation = null;
            foreach (var (instance, entity) in SystemAPI.Query<RefRO<SituationInstance>>().WithEntityAccess())
            {
                if (instance.ValueRO.Phase != SituationPhase.Finished && 
                    instance.ValueRO.Phase != SituationPhase.Failed &&
                    instance.ValueRO.Phase != SituationPhase.Aborted)
                {
                    firstSituation = entity;
                    break;
                }
            }

            if (!firstSituation.HasValue)
            {
                UnityEngine.Debug.Log("[Space4xNarrativeChoiceInput] No active situation found");
                return;
            }

            // Get inbox singleton
            if (!SystemAPI.TryGetSingletonEntity<SituationChoice>(out var inboxEntity))
            {
                return;
            }

            var choiceBuffer = state.EntityManager.GetBuffer<SituationChoice>(inboxEntity);

            // Write choice entry (option 0)
            choiceBuffer.Add(new SituationChoice
            {
                SituationEntity = firstSituation.Value,
                OptionIndex = 0,
                ChoiceId = default
            });

            UnityEngine.Debug.Log($"[Space4xNarrativeChoiceInput] Wrote choice option 0 for situation {firstSituation.Value.Index}");
        }
    }
}

