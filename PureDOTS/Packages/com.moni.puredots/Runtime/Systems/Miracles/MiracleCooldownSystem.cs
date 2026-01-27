using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Updates miracle cooldown timers every frame.
    /// Decrements RemainingSeconds and handles charge recharge.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup))]
    [UpdateAfter(typeof(MiracleActivationSystem))]
    public partial struct MiracleCooldownSystem : ISystem
    {
        private TimeAwareController _controller;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _controller = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp,
                TimeAwareExecutionOptions.SkipWhenPaused);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }

            if (!_controller.TryBegin(timeState, rewindState, out var context))
            {
                return;
            }

            // Only process in Record mode
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update all cooldown buffers
            var bufferLookup = SystemAPI.GetBufferLookup<MiracleCooldown>(false);
            bufferLookup.Update(ref state);
            
            var entities = SystemAPI.QueryBuilder().WithAll<MiracleCooldown>().Build().ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (!bufferLookup.HasBuffer(entity))
                    continue;
                    
                var cooldowns = bufferLookup[entity];
                for (int i = 0; i < cooldowns.Length; i++)
                {
                    var cooldown = cooldowns[i];
                    cooldown.RemainingSeconds = math.max(0f, cooldown.RemainingSeconds - deltaTime);
                    
                    // Future: Handle charge recharge when cooldown completes
                    // For MVP, charges are only restored manually or on activation
                    
                    cooldowns[i] = cooldown;
                }
            }
            entities.Dispose();
        }
    }
}





