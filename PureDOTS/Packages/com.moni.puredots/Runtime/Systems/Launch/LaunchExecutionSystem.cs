using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Launch;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace PureDOTS.Systems.Launch
{
    /// <summary>
    /// Executes pending launches when their scheduled tick arrives.
    /// Applies initial velocity to payload entities.
    /// Only runs in Record mode (rewind-safe).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LaunchRequestIntakeSystem))]
    public partial struct LaunchExecutionSystem : ISystem
    {
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Only execute launches in Record mode
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            _velocityLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process all launchers with queue entries
            foreach (var tuple in SystemAPI
                         .Query<DynamicBuffer<LaunchQueueEntry>, RefRW<LauncherState>>()
                         .WithEntityAccess())
            {
                var queueBuffer = tuple.Item1;
                var launcherState = tuple.Item2;
                var entity = tuple.Item3;

                var stateVal = launcherState.ValueRO;
                bool stateChanged = false;

                for (int i = 0; i < queueBuffer.Length; i++)
                {
                    var entry = queueBuffer[i];

                    // Skip if not pending or not time yet
                    if (entry.State != LaunchEntryState.Pending)
                        continue;

                    if (entry.ScheduledTick > currentTick)
                        continue;

                    // Execute launch
                    var payload = entry.PayloadEntity;

                    if (state.EntityManager.Exists(payload))
                    {
                        // Apply velocity if entity has PhysicsVelocity
                        if (_velocityLookup.HasComponent(payload))
                        {
                            var velocity = _velocityLookup[payload];
                            velocity.Linear = entry.InitialVelocity;
                            _velocityLookup[payload] = velocity;
                        }

                        // Add launched projectile tag
                        ecb.AddComponent(payload, new LaunchedProjectileTag
                        {
                            LaunchTick = currentTick,
                            SourceLauncher = entity
                        });
                    }

                    // Mark as launched
                    entry.State = LaunchEntryState.Launched;
                    queueBuffer[i] = entry;

                    stateVal.LastLaunchTick = currentTick;
                    stateVal.Version++;
                    stateChanged = true;
                }

                if (stateChanged)
                {
                    launcherState.ValueRW = stateVal;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Cleans up consumed launch queue entries.
    /// Runs after execution to compact queues.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LaunchExecutionSystem))]
    public partial struct LaunchCleanupSystem : ISystem
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
            var currentTick = timeState.Tick;

            // Clean up launched entries (mark as consumed after 1 tick)
            foreach (var tuple in SystemAPI
                         .Query<DynamicBuffer<LaunchQueueEntry>, RefRW<LauncherState>>())
            {
                var queueBuffer = tuple.Item1;
                var launcherState = tuple.Item2;

                var stateVal = launcherState.ValueRO;
                bool changed = false;

                // Mark launched entries as consumed
                for (int i = 0; i < queueBuffer.Length; i++)
                {
                    var entry = queueBuffer[i];
                    if (entry.State == LaunchEntryState.Launched)
                    {
                        entry.State = LaunchEntryState.Consumed;
                        queueBuffer[i] = entry;
                        changed = true;
                    }
                }

                // Remove consumed entries (iterate backwards)
                for (int i = queueBuffer.Length - 1; i >= 0; i--)
                {
                    if (queueBuffer[i].State == LaunchEntryState.Consumed)
                    {
                        queueBuffer.RemoveAt(i);
                        stateVal.QueueCount--;
                        changed = true;
                    }
                }

                if (changed)
                {
                    stateVal.Version++;
                    launcherState.ValueRW = stateVal;
                }
            }
        }
    }
}

