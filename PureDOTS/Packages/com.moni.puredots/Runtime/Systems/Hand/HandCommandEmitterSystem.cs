using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Hand
{
    /// <summary>
    /// Ensures HandCommand buffer entries have proper tick stamps.
    /// Runs after state machine to stamp any commands emitted.
    /// Verb systems process only commands where Command.Tick == CurrentTick.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct HandCommandEmitterSystem : ISystem
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
            uint currentTick = timeState.Tick;

            // Find hand entity with HandCommand buffer
            foreach (var (commandBuffer, entity) in SystemAPI.Query<DynamicBuffer<HandCommand>>().WithEntityAccess())
            {
                var buffer = commandBuffer;
                // Stamp any commands that don't have a tick yet (shouldn't happen if state machine does it correctly)
                for (int i = 0; i < buffer.Length; i++)
                {
                    var cmd = buffer[i];
                    if (cmd.Tick == 0)
                    {
                        cmd.Tick = currentTick;
                        buffer[i] = cmd;
                    }
                }
            }
        }
    }
}

