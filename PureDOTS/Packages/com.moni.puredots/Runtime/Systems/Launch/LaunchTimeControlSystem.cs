using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Launch;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Launch
{
    /// <summary>
    /// Bridges launch/menu commands into the core time control pipeline.
    /// Intended for shared UI (main menu, in-game overlay) to drive pause/speed/rewind without touching game sim code.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(PureDOTS.Systems.CoreSingletonBootstrapSystem))]
    [UpdateAfter(typeof(PureDOTS.Systems.TimeSettingsConfigSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.TimeScaleCommandSystem))]
    public partial struct LaunchTimeControlSystem : ISystem
    {
        private const uint SourceId = 0x4D454E55u; // 'MENU'
        private const byte DefaultPriority = 200;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LaunchRootTag>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TickTimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<LaunchRootTag>();
            if (!state.EntityManager.HasBuffer<LaunchCommand>(root))
            {
                return;
            }

            var launchCommands = state.EntityManager.GetBuffer<LaunchCommand>(root);
            if (launchCommands.Length == 0)
            {
                return;
            }

            var rewindEntity = SystemAPI.GetSingletonEntity<RewindState>();
            if (!state.EntityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                state.EntityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            var timeCommands = state.EntityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            var tick = SystemAPI.GetSingleton<TickTimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();

            for (int i = 0; i < launchCommands.Length; i++)
            {
                var cmd = launchCommands[i];
                switch (cmd.Type)
                {
                    case LaunchCommandType.TogglePause:
                        Enqueue(timeCommands, tick.IsPaused ? TimeControlCommandType.Resume : TimeControlCommandType.Pause);
                        break;

                    case LaunchCommandType.Pause:
                        Enqueue(timeCommands, TimeControlCommandType.Pause);
                        break;

                    case LaunchCommandType.Resume:
                        Enqueue(timeCommands, TimeControlCommandType.Resume);
                        break;

                    case LaunchCommandType.StepTicks:
                        {
                            uint step = cmd.Data0 > 0 ? cmd.Data0 : 1u;
                            timeCommands.Add(new TimeControlCommand
                            {
                                Type = TimeControlCommandType.StepTicks,
                                UintParam = step,
                                Scope = TimeControlScope.Global,
                                Source = TimeControlSource.Player,
                                PlayerId = 0,
                                SourceId = SourceId,
                                Priority = DefaultPriority
                            });
                        }
                        break;

                    case LaunchCommandType.SpeedNormal:
                        EnqueueSpeed(timeCommands, TimeScalePresets.Normal);
                        break;

                    case LaunchCommandType.SlowMo:
                        EnqueueSpeed(timeCommands, TimeScalePresets.HalfSpeed);
                        break;

                    case LaunchCommandType.FastForward:
                        EnqueueSpeed(timeCommands, TimeScalePresets.Fast);
                        break;

                    case LaunchCommandType.SetSpeed:
                        EnqueueSpeed(timeCommands, cmd.Data1);
                        break;

                    case LaunchCommandType.RewindToggle:
                        Enqueue(timeCommands, rewind.Mode == RewindMode.Rewind
                            ? TimeControlCommandType.StopRewind
                            : TimeControlCommandType.StartRewind, ResolveRewindTargetTick(tick, 0));
                        break;

                    case LaunchCommandType.StartRewind:
                        Enqueue(timeCommands, TimeControlCommandType.StartRewind, ResolveRewindTargetTick(tick, cmd.Data0));
                        break;

                    case LaunchCommandType.StopRewind:
                        Enqueue(timeCommands, TimeControlCommandType.StopRewind);
                        break;

                    case LaunchCommandType.ScrubToTick:
                        Enqueue(timeCommands, TimeControlCommandType.ScrubTo, cmd.Data0);
                        break;
                }
            }
        }

        private static void Enqueue(DynamicBuffer<TimeControlCommand> buffer, TimeControlCommandType type)
        {
            buffer.Add(new TimeControlCommand
            {
                Type = type,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                SourceId = SourceId,
                Priority = DefaultPriority
            });
        }

        private static void Enqueue(DynamicBuffer<TimeControlCommand> buffer, TimeControlCommandType type, uint uintParam)
        {
            buffer.Add(new TimeControlCommand
            {
                Type = type,
                UintParam = uintParam,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                SourceId = SourceId,
                Priority = DefaultPriority
            });
        }

        private static void EnqueueSpeed(DynamicBuffer<TimeControlCommand> buffer, float speed)
        {
            buffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.SetSpeed,
                FloatParam = math.clamp(speed, TimeControlLimits.DefaultMinSpeed, TimeControlLimits.DefaultMaxSpeed),
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.Player,
                PlayerId = 0,
                SourceId = SourceId,
                Priority = DefaultPriority
            });
        }

        private static uint ResolveRewindTargetTick(in TickTimeState tick, uint ticksBack)
        {
            uint effectiveBack = ticksBack;
            if (effectiveBack == 0)
            {
                float dt = math.max(1e-4f, tick.FixedDeltaTime);
                effectiveBack = (uint)math.max(1f, math.round((1f / dt) * 3f)); // 3 seconds
            }

            return tick.Tick > effectiveBack ? tick.Tick - effectiveBack : 0u;
        }
    }
}

