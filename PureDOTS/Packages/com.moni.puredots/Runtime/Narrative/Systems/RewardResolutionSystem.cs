using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Narrative;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Narrative
{
    /// <summary>
    /// Translates generic effect requests into reward signals.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct RewardResolutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<NarrativeEffectRequest>(out var effectEntity))
            {
                return;
            }

            var effectBuffer = state.EntityManager.GetBuffer<NarrativeEffectRequest>(effectEntity);

            if (!SystemAPI.TryGetSingletonEntity<NarrativeRewardSignal>(out var rewardEntity))
            {
                return;
            }

            var rewardBuffer = state.EntityManager.GetBuffer<NarrativeRewardSignal>(rewardEntity);

            // Process effect requests
            for (int i = effectBuffer.Length - 1; i >= 0; i--)
            {
                var effect = effectBuffer[i];

                // Translate effect to reward signal(s)
                // One-to-many mapping if needed
                if (effect.EffectType == NarrativeRegistryBuilder.EffectTypeAddResource)
                {
                    // Effect: AddResource -> Reward: ResourceDelta
                    rewardBuffer.Add(new NarrativeRewardSignal
                    {
                        RewardType = 0, // ResourceDelta
                        Target = effect.SituationEntity,
                        Amount = effect.ParamA,
                        SourceId = default // Could be derived from situation
                    });
                }
                else if (effect.EffectType == NarrativeRegistryBuilder.EffectTypeModifyOpinion)
                {
                    // Effect: ModifyOpinion -> Reward: OpinionDelta
                    rewardBuffer.Add(new NarrativeRewardSignal
                    {
                        RewardType = 1, // OpinionDelta
                        Target = effect.SituationEntity,
                        Amount = effect.ParamA,
                        SourceId = default
                    });
                }
                else if (effect.EffectType == NarrativeRegistryBuilder.EffectTypeSetFlag)
                {
                    // Effect: SetFlag -> Reward: FlagSet (or no reward, just internal state)
                    // Could emit a reward signal for flag changes if needed
                }
                // Add more effect types as needed

                // Remove processed effect
                effectBuffer.RemoveAt(i);
            }
        }
    }
}

