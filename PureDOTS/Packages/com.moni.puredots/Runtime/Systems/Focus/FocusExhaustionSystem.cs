using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Focus;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Focus
{
    /// <summary>
    /// Tracks exhaustion when focus is low and abilities are active.
    /// Triggers coma when exhaustion reaches threshold.
    /// Handles coma recovery.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FocusSystemGroup))]
    [UpdateAfter(typeof(FocusAbilitySystem))]
    public partial struct FocusExhaustionSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.DeltaTime;
            uint currentTick = timeState.Tick;

            // Get config or use defaults
            FocusConfig config;
            if (SystemAPI.TryGetSingleton<FocusConfig>(out var configSingleton))
            {
                config = configSingleton;
            }
            else
            {
                config = new FocusConfig
                {
                    ComaThreshold = 100,
                    BreakdownWarningThreshold = 80,
                    ExhaustionDecayRate = 5f,
                    ExhaustionGainRate = 10f,
                    SafeFocusThreshold = 0.1f,
                    ComaDuration = 30f
                };
            }

            new ExhaustionJob
            {
                DeltaTime = deltaTime,
                CurrentTick = currentTick,
                Config = config
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ExhaustionJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public FocusConfig Config;

            void Execute(
                Entity entity,
                ref EntityFocus focus,
                ref DynamicBuffer<FocusExhaustionEvent> events)
            {
                byte previousLevel = focus.ExhaustionLevel;

                if (focus.IsInComa)
                {
                    // In coma - slowly recover
                    float recovery = Config.ExhaustionDecayRate * 2f * DeltaTime; // 2x recovery in coma
                    if (focus.ExhaustionLevel > (byte)recovery)
                    {
                        focus.ExhaustionLevel -= (byte)recovery;
                    }
                    else
                    {
                        focus.ExhaustionLevel = 0;
                    }

                    // Check if recovered from coma
                    if (focus.ExhaustionLevel < Config.BreakdownWarningThreshold)
                    {
                        focus.IsInComa = false;
                        focus.CurrentFocus = focus.MaxFocus * 0.25f; // Wake with 25% focus

                        events.Add(new FocusExhaustionEvent
                        {
                            AffectedEntity = entity,
                            EventType = FocusExhaustionEventType.ComaExited,
                            PreviousLevel = previousLevel,
                            NewLevel = focus.ExhaustionLevel,
                            Tick = CurrentTick
                        });
                    }
                }
                else
                {
                    // Calculate focus percentage
                    float focusPercent = focus.MaxFocus > 0 ? focus.CurrentFocus / focus.MaxFocus : 0f;

                    if (focusPercent < Config.SafeFocusThreshold && focus.TotalDrainRate > 0f)
                    {
                        // Low focus with active abilities - gain exhaustion
                        float exhaustionGain = Config.ExhaustionGainRate * DeltaTime;
                        int newLevel = focus.ExhaustionLevel + (int)exhaustionGain;
                        focus.ExhaustionLevel = (byte)(newLevel > 255 ? 255 : newLevel);
                    }
                    else if (focus.TotalDrainRate == 0f)
                    {
                        // No active abilities - recover exhaustion
                        float recovery = Config.ExhaustionDecayRate * DeltaTime;
                        if (focus.ExhaustionLevel > (byte)recovery)
                        {
                            focus.ExhaustionLevel -= (byte)recovery;
                        }
                        else
                        {
                            focus.ExhaustionLevel = 0;
                        }
                    }

                    // Check thresholds
                    if (focus.ExhaustionLevel >= Config.ComaThreshold && !focus.IsInComa)
                    {
                        // Enter coma
                        focus.IsInComa = true;
                        focus.CurrentFocus = 0f;
                        focus.TotalDrainRate = 0f;

                        events.Add(new FocusExhaustionEvent
                        {
                            AffectedEntity = entity,
                            EventType = FocusExhaustionEventType.ComaEntered,
                            PreviousLevel = previousLevel,
                            NewLevel = focus.ExhaustionLevel,
                            Tick = CurrentTick
                        });
                    }
                    else if (focus.ExhaustionLevel >= Config.BreakdownWarningThreshold &&
                             previousLevel < Config.BreakdownWarningThreshold)
                    {
                        // Crossed warning threshold
                        events.Add(new FocusExhaustionEvent
                        {
                            AffectedEntity = entity,
                            EventType = FocusExhaustionEventType.BreakdownWarning,
                            PreviousLevel = previousLevel,
                            NewLevel = focus.ExhaustionLevel,
                            Tick = CurrentTick
                        });
                    }
                }

                // Emit level change events
                if (focus.ExhaustionLevel != previousLevel && !focus.IsInComa)
                {
                    events.Add(new FocusExhaustionEvent
                    {
                        AffectedEntity = entity,
                        EventType = focus.ExhaustionLevel > previousLevel
                            ? FocusExhaustionEventType.ExhaustionIncreased
                            : FocusExhaustionEventType.ExhaustionDecreased,
                        PreviousLevel = previousLevel,
                        NewLevel = focus.ExhaustionLevel,
                        Tick = CurrentTick
                    });
                }
            }
        }
    }
}

