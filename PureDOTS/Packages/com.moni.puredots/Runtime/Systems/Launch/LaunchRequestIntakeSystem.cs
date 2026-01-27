using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Launch;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Launch
{
    /// <summary>
    /// Processes LaunchRequest buffers and populates LaunchQueueEntry buffers on launchers.
    /// Only runs in Record mode (rewind-safe).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(LaunchExecutionSystem))]
    public partial struct LaunchRequestIntakeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Only process requests in Record mode
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Process all entities with LaunchRequest buffers
            foreach (var (requestBuffer, config, launcherState, queueBuffer, entity) in
                SystemAPI.Query<DynamicBuffer<LaunchRequest>, RefRO<LauncherConfig>, RefRW<LauncherState>, DynamicBuffer<LaunchQueueEntry>>()
                    .WithEntityAccess())
            {
                if (requestBuffer.Length == 0)
                    continue;

                var configVal = config.ValueRO;
                var stateVal = launcherState.ValueRO;

                // Process each request
                for (int i = 0; i < requestBuffer.Length; i++)
                {
                    var request = requestBuffer[i];

                    // Skip if queue is full
                    if (stateVal.QueueCount >= configVal.MaxQueueSize)
                        continue;

                    // Determine launch tick
                    uint scheduledTick = request.LaunchTick;
                    if (scheduledTick == 0)
                    {
                        // Immediate = next tick after cooldown
                        scheduledTick = currentTick + 1;
                        if (stateVal.LastLaunchTick > 0)
                        {
                            var earliestTick = stateVal.LastLaunchTick + configVal.CooldownTicks;
                            if (scheduledTick < earliestTick)
                                scheduledTick = earliestTick;
                        }
                    }

                    // Add to queue
                    queueBuffer.Add(new LaunchQueueEntry
                    {
                        PayloadEntity = request.PayloadEntity,
                        ScheduledTick = scheduledTick,
                        InitialVelocity = request.InitialVelocity,
                        State = LaunchEntryState.Pending
                    });

                    stateVal.QueueCount++;
                    stateVal.Version++;
                }

                // Update state
                launcherState.ValueRW = stateVal;

                // Clear processed requests
                requestBuffer.Clear();
            }
        }
    }
}





