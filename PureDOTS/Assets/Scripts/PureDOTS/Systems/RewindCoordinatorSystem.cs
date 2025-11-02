using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Orchestrates rewind state transitions and manages playback.
    /// Replaces UnifiedRewindSystem coordination logic.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(TimeTickSystem))]
    public partial struct RewindCoordinatorSystem : ISystem
    {
        private float _playbackAccumulator;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<HistorySettings>();

            _playbackAccumulator = 0f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingletonRW<RewindState>();
            var timeState = SystemAPI.GetSingletonRW<TimeState>();
            // Process time control commands
            ProcessCommands(ref state, ref rewindState.ValueRW, ref timeState.ValueRW);

            // Handle rewind state machine
            switch (rewindState.ValueRO.Mode)
            {
                case RewindMode.Record:
                    HandleRecordMode(ref state, ref timeState.ValueRW);
                    break;

                case RewindMode.Playback:
                    HandlePlaybackMode(ref state, ref rewindState.ValueRW,
                        ref timeState.ValueRW);
                    break;

                case RewindMode.CatchUp:
                    HandleCatchUpMode(ref state, ref rewindState.ValueRW,
                        ref timeState.ValueRW);
                    break;
            }
        }

        [BurstCompile]
        private void ProcessCommands(ref SystemState state,
            ref RewindState rewindState, ref TimeState timeState)
        {
            var commandEntity = SystemAPI.GetSingletonEntity<RewindState>();
            if (!state.EntityManager.HasBuffer<TimeControlCommand>(commandEntity))
                return;

            var commands = state.EntityManager.GetBuffer<TimeControlCommand>(commandEntity);

            for (int i = 0; i < commands.Length; i++)
            {
                var cmd = commands[i];
                switch (cmd.Type)
                {
                    case TimeControlCommand.CommandType.Pause:
                        timeState.IsPaused = true;
                        break;

                    case TimeControlCommand.CommandType.Resume:
                        timeState.IsPaused = false;
                        break;

                    case TimeControlCommand.CommandType.SetSpeed:
                        timeState.CurrentSpeedMultiplier = math.clamp(cmd.FloatParam, 0.1f, 5f);
                        break;

                    case TimeControlCommand.CommandType.StartRewind:
                        if (rewindState.Mode == RewindMode.Record)
                        {
                            StartRewind(ref rewindState, timeState.Tick, cmd.UintParam);
                        }
                        break;

                    case TimeControlCommand.CommandType.StopRewind:
                        if (rewindState.Mode == RewindMode.Playback)
                        {
                            StopRewind(ref rewindState, ref timeState);
                        }
                        break;

                    case TimeControlCommand.CommandType.ScrubTo:
                        if (rewindState.Mode == RewindMode.Playback)
                        {
                            rewindState.TargetTick = cmd.UintParam;
                        }
                        break;
                }
            }

            commands.Clear();
        }

        [BurstCompile]
        private void HandleRecordMode(ref SystemState state, ref TimeState timeState)
        {
            // In record mode, we let TimeTickSystem advance normally
            // Add PlaybackGuardTag removal if any entities have it
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (tag, entity) in SystemAPI.Query<RefRO<PlaybackGuardTag>>()
                .WithEntityAccess())
            {
                ecb.RemoveComponent<PlaybackGuardTag>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private void HandlePlaybackMode(ref SystemState state,
            ref RewindState rewindState, ref TimeState timeState)
        {
            // Pause normal simulation during playback
            timeState.IsPaused = true;

            // Add PlaybackGuardTag to all rewindable entities
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (tag, entity) in SystemAPI.Query<RefRO<RewindableTag>>()
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

            float tickInterval = 1f / rewindState.PlaybackTicksPerSecond;

            while (_playbackAccumulator >= tickInterval)
            {
                _playbackAccumulator -= tickInterval;

                // Move playback tick toward target
                if (rewindState.PlaybackTick < rewindState.TargetTick)
                {
                    rewindState.PlaybackTick++;
                }
                else if (rewindState.PlaybackTick > rewindState.TargetTick)
                {
                    rewindState.PlaybackTick--;
                }
                else
                {
                    // Reached target, transition to catch-up mode
                    rewindState.Mode = RewindMode.CatchUp;
                    timeState.Tick = rewindState.TargetTick;
                    _playbackAccumulator = 0f;
                    break;
                }
            }
        }

        [BurstCompile]
        private void HandleCatchUpMode(ref SystemState state,
            ref RewindState rewindState, ref TimeState timeState)
        {
            // In catch-up mode, rapidly advance to current time
            uint currentTick = rewindState.StartTick;

            // Advance up to 6 ticks per frame to catch up
            int catchUpSteps = math.min(6, (int)(currentTick - timeState.Tick));

            for (int i = 0; i < catchUpSteps; i++)
            {
                timeState.Tick++;
            }

            // Check if caught up
            if (timeState.Tick >= currentTick)
            {
                rewindState.Mode = RewindMode.Record;
                timeState.IsPaused = false;

                // Remove PlaybackGuardTag
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                foreach (var (tag, entity) in SystemAPI.Query<RefRO<PlaybackGuardTag>>()
                    .WithEntityAccess())
                {
                    ecb.RemoveComponent<PlaybackGuardTag>(entity);
                }
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }

        private void StartRewind(ref RewindState rewindState, uint currentTick, uint targetTick)
        {
            rewindState.Mode = RewindMode.Playback;
            rewindState.StartTick = currentTick;
            rewindState.TargetTick = targetTick;
            rewindState.PlaybackTick = currentTick;
            _playbackAccumulator = 0f;
        }

        private void StopRewind(ref RewindState rewindState, ref TimeState timeState)
        {
            // Transition to catch-up or directly to record
            if (rewindState.PlaybackTick < rewindState.StartTick)
            {
                rewindState.Mode = RewindMode.CatchUp;
                timeState.Tick = rewindState.PlaybackTick;
            }
            else
            {
                rewindState.Mode = RewindMode.Record;
                timeState.IsPaused = false;
            }
        }
    }
}
