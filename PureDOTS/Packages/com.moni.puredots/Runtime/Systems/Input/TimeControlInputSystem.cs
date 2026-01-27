using InputState = PureDOTS.Input.TimeControlInputState;
using RuntimeTimeControlInputState = PureDOTS.Runtime.Components.TimeControlInputState;
using PureDOTS.Runtime.Components;
using PureDOTS.Input;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Input
{
    /// <summary>
    /// Converts player-facing time control input into deterministic commands consumed by <see cref="RewindCoordinatorSystem"/>.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // Removed invalid UpdateAfter: CopyInputToEcsSystem runs in CameraInputSystemGroup; Simulation systems already execute afterward.
    public partial struct TimeControlInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeControlSingletonTag>();
            state.RequireForUpdate<TimeControlConfig>();
            state.RequireForUpdate<RuntimeTimeControlInputState>();
            state.RequireForUpdate<TimeControlCommand>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RtsInputSingletonTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeStateRO = SystemAPI.GetSingleton<TimeState>();

            // Process RTS time control events
            Entity rtsInputEntity = SystemAPI.GetSingletonEntity<RtsInputSingletonTag>();
            if (state.EntityManager.HasBuffer<TimeControlInputEvent>(rtsInputEntity))
            {
                var rtsEventBuffer = state.EntityManager.GetBuffer<TimeControlInputEvent>(rtsInputEntity);
                ProcessRtsTimeControlEvents(ref state, rtsEventBuffer, timeStateRO);
                rtsEventBuffer.Clear();
            }

            foreach (var (inputRef, configRO, commandBuffer, entity) in SystemAPI
                .Query<RefRW<RuntimeTimeControlInputState>, RefRO<TimeControlConfig>, DynamicBuffer<TimeControlCommand>>()
                .WithAll<TimeControlSingletonTag>()
                .WithEntityAccess())
            {
                var input = inputRef.ValueRO;
                var config = configRO.ValueRO;

                if (input.PauseToggleTriggered != 0)
                {
                    bool currentlyPaused = timeStateRO.IsPaused;
                    commandBuffer.Add(new TimeControlCommand
                    {
                        Type = currentlyPaused ? TimeControlCommandType.Resume : TimeControlCommandType.Pause
                    });
                }

                if (input.StepDownTriggered != 0)
                {
                    // When paused, treat as a single step; otherwise drop into slow-motion.
                    commandBuffer.Add(new TimeControlCommand
                    {
                        Type = timeStateRO.IsPaused
                            ? TimeControlCommandType.StepTicks
                            : TimeControlCommandType.SetSpeed,
                        FloatParam = math.max(0.1f, config.SlowMotionSpeed),
                        UintParam = 1
                    });
                }

                if (input.StepUpTriggered != 0)
                {
                    commandBuffer.Add(new TimeControlCommand
                    {
                        Type = timeStateRO.IsPaused
                            ? TimeControlCommandType.StepTicks
                            : TimeControlCommandType.SetSpeed,
                        FloatParam = math.max(0.1f, config.FastForwardSpeed),
                        UintParam = 1
                    });
                }

                if (input.RewindPressedThisFrame != 0)
                {
                    uint currentTick = timeStateRO.Tick;
                    uint depthTicks = (uint)math.max(1f, math.round(3f / math.max(0.0001f, timeStateRO.FixedDeltaTime)));
                    uint targetTick = currentTick > depthTicks ? currentTick - depthTicks : 0u;

                    commandBuffer.Add(new TimeControlCommand
                    {
                        Type = TimeControlCommandType.StartRewind,
                        UintParam = targetTick
                    });
                }

                if (input.EnterGhostPreview != 0 && input.RewindSpeedLevel > 0)
                {
                    commandBuffer.Add(new TimeControlCommand
                    {
                        Type = TimeControlCommandType.StopRewind
                    });
                }

                // Reset one-shot fields but keep speed level if key remains held
                inputRef.ValueRW = new RuntimeTimeControlInputState
                {
                    SampleTick = input.SampleTick,
                    RewindSpeedLevel = input.RewindHeld != 0 ? input.RewindSpeedLevel : (byte)0,
                    RewindHeld = input.RewindHeld
                };
            }
        }

        private void ProcessRtsTimeControlEvents(ref SystemState state, in DynamicBuffer<TimeControlInputEvent> events, in TimeState timeState)
        {
            foreach (var (configRO, commandBuffer, entity) in SystemAPI
                .Query<RefRO<TimeControlConfig>, DynamicBuffer<TimeControlCommand>>()
                .WithAll<TimeControlSingletonTag>()
                .WithEntityAccess())
            {
                var config = configRO.ValueRO;

                for (int i = 0; i < events.Length; i++)
                {
                    var rtsEvent = events[i];

                    switch (rtsEvent.Kind)
                    {
                        case TimeControlCommandKind.TogglePause:
                            bool currentlyPaused = timeState.IsPaused;
                            commandBuffer.Add(new TimeControlCommand
                            {
                                Type = currentlyPaused ? TimeControlCommandType.Resume : TimeControlCommandType.Pause
                            });
                            break;

                        case TimeControlCommandKind.SetScale:
                            commandBuffer.Add(new TimeControlCommand
                            {
                                Type = TimeControlCommandType.SetSpeed,
                                FloatParam = rtsEvent.FloatParam
                            });
                            break;

                        case TimeControlCommandKind.EnterRewind:
                            uint currentTick = timeState.Tick;
                            uint depthTicks = (uint)math.max(1f, math.round(3f / math.max(0.0001f, timeState.FixedDeltaTime)));
                            uint targetTick = currentTick > depthTicks ? currentTick - depthTicks : 0u;
                            commandBuffer.Add(new TimeControlCommand
                            {
                                Type = TimeControlCommandType.StartRewind,
                                UintParam = targetTick
                            });
                            break;

                        case TimeControlCommandKind.ExitRewind:
                            commandBuffer.Add(new TimeControlCommand
                            {
                                Type = TimeControlCommandType.StopRewind
                            });
                            break;

                        case TimeControlCommandKind.StepTicks:
                            commandBuffer.Add(new TimeControlCommand
                            {
                                Type = TimeControlCommandType.StepTicks,
                                UintParam = (uint)rtsEvent.IntParam
                            });
                            break;
                    }
                }
            }
        }
    }
}
