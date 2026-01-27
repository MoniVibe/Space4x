using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Processes TimeControlCommand entries related to timescale scheduling.
    /// Handles AddTimeScaleEntry and RemoveTimeScaleEntry commands.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(TimeSettingsConfigSystem))]
    [UpdateBefore(typeof(TimeScaleResolutionSystem))]
    [UpdateBefore(typeof(RewindCoordinatorSystem))]
    [UpdateBefore(typeof(TimeTickSystem))]
    public partial struct TimeScaleCommandSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeScaleScheduleState>();
            state.RequireForUpdate<TimeControlCommand>();
            state.RequireForUpdate<TickTimeState>(); // Required for GetSingleton<TickTimeState>()
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            var scheduleEntity = SystemAPI.GetSingletonEntity<TimeScaleScheduleState>();
            var scheduleStateHandle = SystemAPI.GetSingletonRW<TimeScaleScheduleState>();
            ref var scheduleState = ref scheduleStateHandle.ValueRW;

            if (!state.EntityManager.HasBuffer<TimeScaleEntry>(scheduleEntity))
            {
                return;
            }

            var entries = state.EntityManager.GetBuffer<TimeScaleEntry>(scheduleEntity);
            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();

            // Get config or use defaults
            var config = TimeScaleConfig.CreateDefault();
            if (SystemAPI.TryGetSingleton<TimeScaleConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            // Get feature flags for MP validation
            bool hasFlags = SystemAPI.TryGetSingleton<TimeSystemFeatureFlags>(out var flags);
            TimeSimulationMode simulationMode = TimeSimulationMode.SinglePlayer;
            if (hasFlags)
            {
                simulationMode = flags.SimulationMode;
            }

            // Process time control commands from all entities that have the command buffer
            foreach (var (commandBuffer, entity) in SystemAPI.Query<DynamicBuffer<TimeControlCommand>>()
                .WithEntityAccess())
            {
                for (int i = commandBuffer.Length - 1; i >= 0; i--)
                {
                    var cmd = commandBuffer[i];
                    
                    // Validate command scope for current simulation mode
                    // In single-player, Player scope should be treated as Global
                    if (cmd.Scope == TimeControlScope.Player && simulationMode == TimeSimulationMode.SinglePlayer)
                    {
                        // Log warning and treat as Global for now
                        // TODO: In MP, server will validate PlayerId and apply only to player's entities
                        cmd.Scope = TimeControlScope.Global;
                    }
                    
                    // Guard: Check if command is allowed in multiplayer mode
                    if (hasFlags)
                    {
                        var result = TimeMultiplayerGuards.CheckCommandAllowed(flags, cmd, null);
                        if (result != TimeCommandAuthorityResult.Ok)
                        {
                            // For now, skip the command in MP scenarios.
                            // TODO: Log one-time warning in MP mode
                            continue;
                        }
                    }
                    
                    // TODO: In multiplayer, server validates PlayerId and applies commands to player's time authority
                    // - Server validates PlayerId matches command issuer
                    // - Commands with invalid PlayerId are rejected
                    // - Per-player time scale entries are maintained separately
                    
                    switch (cmd.Type)
                    {
                        case TimeControlCommandType.AddTimeScaleEntry:
                            ProcessAddEntry(ref entries, ref scheduleState, cmd, tickTimeState.Tick, config);
                            commandBuffer.RemoveAt(i);
                            break;

                        case TimeControlCommandType.RemoveTimeScaleEntry:
                            ProcessRemoveEntry(ref entries, cmd);
                            commandBuffer.RemoveAt(i);
                            break;

                        case TimeControlCommandType.Pause:
                            // Create a pause entry via schedule system
                            var pauseEntry = TimeScaleEntry.CreatePause(
                                scheduleState.NextEntryId++,
                                ConvertSource(cmd.Source),
                                cmd.SourceId,
                                cmd.Priority > 0 ? cmd.Priority : (byte)200 // High priority for explicit pause
                            );
                            entries.Add(pauseEntry);
                            // Don't remove the command - let RewindCoordinatorSystem also process it for legacy compat
                            break;

                        case TimeControlCommandType.Resume:
                            // Remove pause entries from the same source
                            RemovePauseEntries(ref entries, cmd.SourceId);
                            // Don't remove - let RewindCoordinatorSystem handle it
                            break;

                        case TimeControlCommandType.SetSpeed:
                            // Update or add a speed entry
                            ProcessSetSpeed(ref entries, ref scheduleState, cmd, tickTimeState.Tick, config);
                            // Don't remove - let RewindCoordinatorSystem handle it for legacy compat
                            break;
                    }
                }
            }
        }

        private static void ProcessAddEntry(ref DynamicBuffer<TimeScaleEntry> entries,
            ref TimeScaleScheduleState scheduleState, in TimeControlCommand cmd, uint currentTick,
            in TimeScaleConfig config)
        {
            var entry = new TimeScaleEntry
            {
                EntryId = scheduleState.NextEntryId++,
                StartTick = cmd.UintParam > 0 ? cmd.UintParam : currentTick,
                EndTick = uint.MaxValue, // Permanent until removed
                Scale = math.clamp(cmd.FloatParam, config.MinScale, config.MaxScale),
                Source = ConvertSource(cmd.Source),
                Priority = cmd.Priority,
                SourceId = cmd.SourceId,
                IsPause = cmd.FloatParam <= 0f
            };
            entries.Add(entry);
        }

        private static void ProcessRemoveEntry(ref DynamicBuffer<TimeScaleEntry> entries, in TimeControlCommand cmd)
        {
            // Remove by entry ID (stored in UintParam) or by source ID
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.EntryId == cmd.UintParam || 
                    (cmd.SourceId != 0 && entry.SourceId == cmd.SourceId))
                {
                    entries.RemoveAt(i);
                }
            }
        }

        private static void ProcessSetSpeed(ref DynamicBuffer<TimeScaleEntry> entries,
            ref TimeScaleScheduleState scheduleState, in TimeControlCommand cmd, uint currentTick,
            in TimeScaleConfig config)
        {
            var source = ConvertSource(cmd.Source);
            
            // Remove existing player speed entries (not pauses)
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.Source == source && !entry.IsPause && entry.SourceId == cmd.SourceId)
                {
                    entries.RemoveAt(i);
                }
            }

            // Add new speed entry
            var newEntry = TimeScaleEntry.CreateSpeed(
                scheduleState.NextEntryId++,
                math.clamp(cmd.FloatParam, config.MinScale, config.MaxScale),
                source,
                cmd.SourceId,
                cmd.Priority > 0 ? cmd.Priority : (byte)100, // Default priority for player speed
                currentTick,
                uint.MaxValue
            );
            entries.Add(newEntry);
        }

        private static void RemovePauseEntries(ref DynamicBuffer<TimeScaleEntry> entries, uint sourceId)
        {
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.IsPause && (sourceId == 0 || entry.SourceId == sourceId))
                {
                    entries.RemoveAt(i);
                }
            }
        }

        private static TimeScaleSource ConvertSource(TimeControlSource source)
        {
            return source switch
            {
                TimeControlSource.Player => TimeScaleSource.Player,
                TimeControlSource.Miracle => TimeScaleSource.Miracle,
                TimeControlSource.Scenario => TimeScaleSource.Scenario,
                TimeControlSource.DevTool => TimeScaleSource.DevTool,
                TimeControlSource.Technology => TimeScaleSource.Technology,
                TimeControlSource.System => TimeScaleSource.SystemPause,
                _ => TimeScaleSource.Default
            };
        }
    }
}
