using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Resolves active timescale entries into a single effective timescale.
    /// Runs early in TimeSystemGroup before tick advancement.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(TimeScaleCommandSystem))]
    // Removed invalid UpdateAfter/Before: ordering relative to CoreSingletonBootstrapSystem and TimeSettingsConfigSystem is governed by group order (OrderFirst).
    public partial struct TimeScaleResolutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeScaleScheduleState>();
            state.RequireForUpdate<TimeScaleEntry>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            var scheduleStateHandle = SystemAPI.GetSingletonRW<TimeScaleScheduleState>();
            var tickTimeHandle = SystemAPI.GetSingletonRW<TickTimeState>();
            var timeHandle = SystemAPI.GetSingletonRW<TimeState>();

            ref var scheduleState = ref scheduleStateHandle.ValueRW;
            ref var tickTimeState = ref tickTimeHandle.ValueRW;
            ref var timeState = ref timeHandle.ValueRW;

            // Get config or use defaults
            var config = TimeScaleConfig.CreateDefault();
            if (SystemAPI.TryGetSingleton<TimeScaleConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            uint currentTick = tickTimeState.Tick;

            // Get the schedule entity and buffer
            var scheduleEntity = SystemAPI.GetSingletonEntity<TimeScaleScheduleState>();
            if (!state.EntityManager.HasBuffer<TimeScaleEntry>(scheduleEntity))
            {
                // No entries, use default scale
                scheduleState.ResolvedScale = config.DefaultScale;
                scheduleState.IsPaused = false;
                scheduleState.ActiveEntryId = 0;
                scheduleState.ActiveSource = TimeScaleSource.Default;
                ApplyResolvedScale(ref tickTimeState, ref timeState, config.DefaultScale, false, config);
                return;
            }

            var entries = state.EntityManager.GetBuffer<TimeScaleEntry>(scheduleEntity);
            
            // Prune expired entries and find winning entry
            PruneAndResolve(ref entries, currentTick, config, ref scheduleState);

            // Apply resolved state
            ApplyResolvedScale(ref tickTimeState, ref timeState, scheduleState.ResolvedScale, 
                scheduleState.IsPaused, config);
        }

        private static void PruneAndResolve(ref DynamicBuffer<TimeScaleEntry> entries, uint currentTick,
            in TimeScaleConfig config, ref TimeScaleScheduleState scheduleState)
        {
            // First pass: prune expired entries
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.EndTick < currentTick && entry.EndTick != uint.MaxValue)
                {
                    entries.RemoveAt(i);
                }
            }

            // If no entries remain, use defaults
            if (entries.Length == 0)
            {
                scheduleState.ResolvedScale = config.DefaultScale;
                scheduleState.IsPaused = false;
                scheduleState.ActiveEntryId = 0;
                scheduleState.ActiveSource = TimeScaleSource.Default;
                return;
            }

            // Second pass: find winning entry (highest priority, pause wins over scale)
            byte highestPriority = 0;
            int winningIndex = -1;
            bool foundPause = false;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                
                // Skip entries not yet active
                if (entry.StartTick > currentTick)
                {
                    continue;
                }

                // Pause entries with same or higher priority always win
                if (entry.IsPause)
                {
                    if (!foundPause || entry.Priority >= highestPriority)
                    {
                        foundPause = true;
                        highestPriority = entry.Priority;
                        winningIndex = i;
                    }
                }
                else if (!foundPause && entry.Priority >= highestPriority)
                {
                    highestPriority = entry.Priority;
                    winningIndex = i;
                }
            }

            // Apply winning entry
            if (winningIndex >= 0)
            {
                var winner = entries[winningIndex];
                scheduleState.IsPaused = winner.IsPause;
                scheduleState.ResolvedScale = winner.IsPause ? 0f : 
                    math.clamp(winner.Scale, config.MinScale, config.MaxScale);
                scheduleState.ActiveEntryId = winner.EntryId;
                scheduleState.ActiveSource = winner.Source;
            }
            else
            {
                // No active entries yet (all have StartTick > currentTick)
                scheduleState.ResolvedScale = config.DefaultScale;
                scheduleState.IsPaused = false;
                scheduleState.ActiveEntryId = 0;
                scheduleState.ActiveSource = TimeScaleSource.Default;
            }
        }

        private static void ApplyResolvedScale(ref TickTimeState tickTimeState, ref TimeState timeState,
            float scale, bool isPaused, in TimeScaleConfig config)
        {
            float clampedScale = math.clamp(scale, config.MinScale, config.MaxScale);
            
            tickTimeState.CurrentSpeedMultiplier = clampedScale;
            tickTimeState.IsPaused = isPaused;
            
            timeState.CurrentSpeedMultiplier = clampedScale;
            timeState.IsPaused = isPaused;
        }
    }
}
