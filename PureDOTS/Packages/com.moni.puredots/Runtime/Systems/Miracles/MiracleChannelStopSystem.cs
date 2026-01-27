using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Miracles;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Stops sustained miracle channeling when input is released or resource depleted.
    /// Cleans up effect entities and resets channel state on casters.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup))]
    [UpdateAfter(typeof(MiracleSustainedTickSystem))]
    public partial struct MiracleChannelStopSystem : ISystem
    {
        private ComponentLookup<MiracleSustainedEffect> _sustainedEffectLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiracleChannelState>();
            _sustainedEffectLookup = state.GetComponentLookup<MiracleSustainedEffect>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            _sustainedEffectLookup.Update(ref state);

            foreach (var (channelStateRef, runtimeState, entity) in SystemAPI
                         .Query<RefRW<MiracleChannelState>, RefRO<MiracleRuntimeStateNew>>()
                         .WithEntityAccess())
            {
                ref var channelState = ref channelStateRef.ValueRW;

                if (channelState.ActiveEffectEntity == Entity.Null)
                {
                    continue;
                }

                // Check for release signal - IsSustained == 0 means input released
                bool shouldStop = runtimeState.ValueRO.IsSustained == 0;

                // TODO: Check resource depletion (M5)
                // bool resourceDepleted = CheckPrayerPool(...);
                // shouldStop = shouldStop || resourceDepleted;

                if (shouldStop)
                {
                    StopChanneling(ref state, ref ecb, entity, ref channelState);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private void StopChanneling(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            Entity casterEntity,
            ref MiracleChannelState channelState)
        {
            // Set IsChanneling = 0 on effect entity and destroy it
            // HasComponent returns false if entity doesn't exist, providing safe check
            if (_sustainedEffectLookup.HasComponent(channelState.ActiveEffectEntity))
            {
                var sustained = _sustainedEffectLookup[channelState.ActiveEffectEntity];
                sustained.IsChanneling = 0;
                ecb.SetComponent(channelState.ActiveEffectEntity, sustained);
                ecb.DestroyEntity(channelState.ActiveEffectEntity);
            }

            // Reset channel state
            channelState.ActiveEffectEntity = Entity.Null;
            channelState.ChannelingId = MiracleId.None;
            channelState.ChannelStartTime = 0f;
            ecb.SetComponent(casterEntity, channelState);
        }
    }
}

