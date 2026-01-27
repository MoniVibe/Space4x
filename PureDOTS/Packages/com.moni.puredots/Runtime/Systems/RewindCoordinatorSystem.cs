using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Orchestrates rewind state transitions and manages playback.
    /// Replaces UnifiedRewindSystem coordination logic.
    /// 
    /// TODO: Migrate from RewindLegacyState to TimeContext + RewindState.
    /// RewindLegacyState is obsolete and will be removed in v2.0.
    /// Migration path: Use TimeContext for time state and RewindState for rewind mode/target.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup), OrderFirst = true)]
    // Removed invalid UpdateAfter attributes: OrderFirst already controls placement in the TimeSystemGroup.
    public partial struct RewindCoordinatorSystem : ISystem
    {
        private float _playbackAccumulator;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
#pragma warning disable CS0618 // RewindLegacyState is obsolete - TODO: Migrate to TimeContext + RewindState
            state.RequireForUpdate<RewindLegacyState>();
#pragma warning restore CS0618
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<HistorySettings>();
            state.RequireForUpdate<InputCommandLogEntry>();
            state.RequireForUpdate<InputCommandLogState>();
            state.RequireForUpdate<SimulationScalars>();

            _playbackAccumulator = 0f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindHandle = SystemAPI.GetSingletonRW<RewindState>();
#pragma warning disable CS0618 // RewindLegacyState is obsolete - TODO: Migrate to TimeContext + RewindState
            var legacyHandle = SystemAPI.GetSingletonRW<RewindLegacyState>();
#pragma warning restore CS0618
            var tickTimeHandle = SystemAPI.GetSingletonRW<TickTimeState>();
            var timeHandle = SystemAPI.GetSingletonRW<TimeState>();

            ref var rewindState = ref rewindHandle.ValueRW;
            ref var legacyState = ref legacyHandle.ValueRW;
            ref var tickTimeState = ref tickTimeHandle.ValueRW;
            ref var timeState = ref timeHandle.ValueRW;

            // Process time control commands
            ProcessCommands(ref state, ref rewindState, ref legacyState, ref tickTimeState);

            // Handle rewind state machine
            switch (rewindState.Mode)
            {
                case RewindMode.Play:
                    HandleRecordMode(ref state, ref tickTimeState);
                    break;

                case RewindMode.Rewind:
                    HandlePlaybackMode(ref state, ref rewindState, ref legacyState, ref tickTimeState);
                    break;

                case RewindMode.Step:
                    HandleCatchUpMode(ref state, ref rewindState, ref legacyState, ref tickTimeState);
                    break;
            }

            SyncLegacyTime(ref tickTimeState, ref timeState);
        }

        [BurstCompile]
        private void ProcessCommands(ref SystemState state,
            ref RewindState rewindState, 
#pragma warning disable CS0618 // RewindLegacyState is obsolete - TODO: Migrate to TimeContext + RewindState
            ref RewindLegacyState legacyState, 
#pragma warning restore CS0618
            ref TickTimeState tickTimeState)
        {
            var commandEntity = SystemAPI.GetSingletonEntity<RewindState>();
            if (!state.EntityManager.HasBuffer<TimeControlCommand>(commandEntity))
                return;

            var commands = state.EntityManager.GetBuffer<TimeControlCommand>(commandEntity);
            var logState = SystemAPI.GetSingletonRW<InputCommandLogState>();
            var logBuffer = SystemAPI.GetSingletonBuffer<InputCommandLogEntry>();

            // Get simulation mode for validation
            TimeSimulationMode simulationMode = TimeSimulationMode.SinglePlayer;
            if (SystemAPI.TryGetSingleton<TimeSystemFeatureFlags>(out var flags))
            {
                simulationMode = flags.SimulationMode;
            }

            for (int i = 0; i < commands.Length; i++)
            {
                var cmd = commands[i];
                
                // Validate command scope for current simulation mode
                // In single-player, Player scope should be treated as Global
                if (cmd.Scope == TimeControlScope.Player && simulationMode == TimeSimulationMode.SinglePlayer)
                {
                    // Log warning and treat as Global for now
                    // TODO: In MP, server will validate PlayerId and apply only to player's entities
                    cmd.Scope = TimeControlScope.Global;
                }
                
                // TODO: In multiplayer, server will:
                // - Validate PlayerId matches command issuer
                // - Apply commands only to entities that belong to that player's authority
                // - Reject commands with invalid PlayerId
                var logEntry = new InputCommandLogEntry
                {
                    Tick = tickTimeState.Tick,
                    Type = (byte)cmd.Type,
                    FloatParam = cmd.FloatParam,
                    UintParam = cmd.UintParam
                };
                TimeLogUtility.AppendCommand(ref logBuffer, ref logState.ValueRW, logEntry);

                switch (cmd.Type)
                {
                    case TimeControlCommandType.Pause:
                        tickTimeState.IsPaused = true;
                        tickTimeState.IsPlaying = false;
                        break;

                    case TimeControlCommandType.Resume:
                        tickTimeState.IsPaused = false;
                        tickTimeState.IsPlaying = true;
                        break;

                    case TimeControlCommandType.SetSpeed:
                        tickTimeState.CurrentSpeedMultiplier = math.clamp(cmd.FloatParam, 
                            TimeControlLimits.DefaultMinSpeed, TimeControlLimits.DefaultMaxSpeed);
                        break;

                    case TimeControlCommandType.StartRewind:
                        if (rewindState.Mode == RewindMode.Play)
                        {
                            var maxDepth = ResolveRewindHorizonTicks(tickTimeState);
                            uint minTick = tickTimeState.Tick > maxDepth ? tickTimeState.Tick - maxDepth : 0u;
                            uint clampedTarget = math.max(cmd.UintParam, minTick);
                            StartRewind(ref rewindState, ref legacyState, ref tickTimeState, clampedTarget);
                        }
                        break;

                    case TimeControlCommandType.StopRewind:
                        if (rewindState.Mode == RewindMode.Rewind)
                        {
                            StopRewind(ref rewindState, ref legacyState, ref tickTimeState);
                        }
                        break;

                    case TimeControlCommandType.ScrubTo:
                        if (rewindState.Mode == RewindMode.Rewind)
                        {
                            rewindState.TargetTick = (int)cmd.UintParam;
                        }
                        break;

                    case TimeControlCommandType.StepTicks:
                        // Allow single/multi tick advance while paused.
                        uint stepCount = math.max(1u, cmd.UintParam == 0 ? 1u : cmd.UintParam);
                        tickTimeState.TargetTick = math.max(tickTimeState.TargetTick, tickTimeState.Tick + stepCount);
                        tickTimeState.IsPlaying = false;
                        tickTimeState.IsPaused = true;
                        break;
                }
            }

            commands.Clear();
        }

        [BurstCompile]
        private void HandleRecordMode(ref SystemState state, ref TickTimeState tickTimeState)
        {
            // In record mode, we let TimeTickSystem advance normally
            // Add PlaybackGuardTag removal if any entities have it
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlaybackGuardTag>>()
                         .WithEntityAccess())
            {
                ecb.RemoveComponent<PlaybackGuardTag>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            if (tickTimeState.TargetTick < tickTimeState.Tick)
            {
                tickTimeState.TargetTick = tickTimeState.Tick;
            }
        }

        [BurstCompile]
        private void HandlePlaybackMode(ref SystemState state,
            ref RewindState rewindState, 
#pragma warning disable CS0618 // RewindLegacyState is obsolete - TODO: Migrate to TimeContext + RewindState
            ref RewindLegacyState legacyState, 
#pragma warning restore CS0618
            ref TickTimeState tickTimeState)
        {
            // Pause normal simulation during playback
            tickTimeState.IsPaused = true;
            tickTimeState.IsPlaying = false;
            uint targetTick = (uint)math.max(0, rewindState.TargetTick);
            tickTimeState.TargetTick = targetTick;
            tickTimeState.Tick = legacyState.PlaybackTick;

            // Add PlaybackGuardTag to all rewindable entities
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<RewindableTag>>()
                         .WithNone<PlaybackGuardTag>()
                         .WithEntityAccess())
            {
                ecb.AddComponent<PlaybackGuardTag>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Advance playback based on TPS
            float deltaTime = SystemAPI.Time.DeltaTime;
            _playbackAccumulator += deltaTime;

            float tickInterval = 1f / legacyState.PlaybackTicksPerSecond;

            while (_playbackAccumulator >= tickInterval)
            {
                _playbackAccumulator -= tickInterval;

                // Move playback tick toward target
                if (legacyState.PlaybackTick < targetTick)
                {
                    legacyState.PlaybackTick++;
                }
                else if (legacyState.PlaybackTick > targetTick)
                {
                    legacyState.PlaybackTick--;
                }
                else
                {
                    // Reached target, transition to catch-up mode
                    rewindState.Mode = RewindMode.Step;
                    tickTimeState.Tick = targetTick;
                    tickTimeState.TargetTick = targetTick;
                    _playbackAccumulator = 0f;
                    break;
                }

                tickTimeState.Tick = legacyState.PlaybackTick;
            }
        }

        [BurstCompile]
        private void HandleCatchUpMode(ref SystemState state,
            ref RewindState rewindState, 
#pragma warning disable CS0618 // RewindLegacyState is obsolete - TODO: Migrate to TimeContext + RewindState
            ref RewindLegacyState legacyState, 
#pragma warning restore CS0618
            ref TickTimeState tickTimeState)
        {
            tickTimeState.IsPlaying = false;
            tickTimeState.IsPaused = false;
            tickTimeState.TargetTick = legacyState.StartTick;

            // In catch-up mode, rapidly advance to current time
            uint currentTick = legacyState.StartTick;

            // Advance up to 6 ticks per frame to catch up
            int catchUpSteps = math.min(6, (int)math.max(0, currentTick - (uint)tickTimeState.Tick));

            for (int i = 0; i < catchUpSteps; i++)
            {
                tickTimeState.Tick++;
            }

            // Check if caught up
            if (tickTimeState.Tick >= currentTick)
            {
                rewindState.Mode = RewindMode.Play;
                tickTimeState.IsPaused = false;
                tickTimeState.IsPlaying = true;
                tickTimeState.TargetTick = tickTimeState.Tick;

                // Remove PlaybackGuardTag
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                foreach (var (_, entity) in SystemAPI.Query<RefRO<PlaybackGuardTag>>()
                             .WithEntityAccess())
                {
                    ecb.RemoveComponent<PlaybackGuardTag>(entity);
                }
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }

        private static void SyncLegacyTime(ref TickTimeState tickTimeState, ref TimeState legacy)
        {
            legacy.Tick = tickTimeState.Tick;
            legacy.FixedDeltaTime = tickTimeState.FixedDeltaTime;
            legacy.DeltaTime = tickTimeState.FixedDeltaTime;
            legacy.CurrentSpeedMultiplier = tickTimeState.CurrentSpeedMultiplier;
            legacy.IsPaused = tickTimeState.IsPaused;
        }

        private void StartRewind(ref RewindState rewindState, 
#pragma warning disable CS0618 // RewindLegacyState is obsolete - TODO: Migrate to TimeContext + RewindState
            ref RewindLegacyState legacyState, 
#pragma warning restore CS0618
            ref TickTimeState tickTimeState, uint targetTick)
        {
            rewindState.Mode = RewindMode.Rewind;
            legacyState.StartTick = tickTimeState.Tick;
            rewindState.TargetTick = (int)targetTick;
            legacyState.PlaybackTick = tickTimeState.Tick;
            tickTimeState.IsPaused = true;
            tickTimeState.IsPlaying = false;
            tickTimeState.TargetTick = targetTick;
            tickTimeState.Tick = legacyState.PlaybackTick;
            _playbackAccumulator = 0f;
        }

        private void StopRewind(ref RewindState rewindState, 
#pragma warning disable CS0618 // RewindLegacyState is obsolete - TODO: Migrate to TimeContext + RewindState
            ref RewindLegacyState legacyState, 
#pragma warning restore CS0618
            ref TickTimeState tickTimeState)
        {
            // Transition to catch-up or directly to record
            if (legacyState.PlaybackTick < legacyState.StartTick)
            {
                rewindState.Mode = RewindMode.Step;
                tickTimeState.Tick = legacyState.PlaybackTick;
            }
            else
            {
                rewindState.Mode = RewindMode.Play;
                tickTimeState.IsPaused = false;
                tickTimeState.IsPlaying = true;
                tickTimeState.TargetTick = tickTimeState.Tick;
            }
        }

        private uint ResolveRewindHorizonTicks(in TickTimeState tickTimeState)
        {
            float tickRate = tickTimeState.FixedDeltaTime > 0f ? 1f / tickTimeState.FixedDeltaTime : 60f;
            uint depth = (uint)math.max(1f, math.round(tickRate * 3f));

            if (SystemAPI.TryGetSingleton<HistorySettings>(out var history))
            {
                float configuredRate = history.DefaultTicksPerSecond > 0f
                    ? history.DefaultTicksPerSecond
                    : tickRate;
                depth = math.max(depth, (uint)math.round(configuredRate * 3f));
            }

            // Apply rewind window multiplier from valve
            if (SystemAPI.TryGetSingleton<SimulationScalars>(out var scalars))
            {
                depth = (uint)math.round(depth * scalars.RewindWindowMult);
            }

            return depth + 2u;
        }
    }
}
