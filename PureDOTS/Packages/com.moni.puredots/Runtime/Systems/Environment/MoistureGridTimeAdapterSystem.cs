using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Serialises and restores moisture grid state across rewind phases to guarantee determinism.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    public partial struct MoistureGridTimeAdapterSystem : ISystem
    {
        private TimeStreamHistory _history;
        private TimeAwareController _controller;
        private uint _lastRecordedTick;
        private uint _horizonTicks;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _history = new TimeStreamHistory(2048, 512, Allocator.Persistent);
            _controller = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp | TimeAwareExecutionPhase.Playback,
                TimeAwareExecutionOptions.SkipWhenPaused);
            _lastRecordedTick = uint.MaxValue;
            _horizonTicks = 0;

            state.RequireForUpdate<MoistureGrid>();
            state.RequireForUpdate<MoistureGridSimulationState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _history.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var horizon = GetHorizonTicks(timeState);

            if (!_controller.TryBegin(timeState, rewindState, out var context))
            {
                return;
            }

            if (context.IsRecordPhase)
            {
                if (context.Time.Tick == _lastRecordedTick)
                {
                    return;
                }

                _lastRecordedTick = context.Time.Tick;
                uint minTick = context.Time.Tick > horizon ? context.Time.Tick - horizon : 0;
                _history.PruneOlderThan(minTick);

                var recordIndex = _history.BeginRecord(context.Time.Tick, out var writer);
                Save(ref state, ref writer);
                _history.EndRecord(recordIndex);
            }
            else if (context.IsCatchUpPhase || context.IsPlaybackPhase)
            {
                uint targetTick = context.Time.Tick;
                if (!_history.TryGet(targetTick, out var bytes))
                {
                    return;
                }

                var reader = new TimeStreamReader(bytes);
                Load(ref state, ref reader);
            }

            if (context.ModeChangedThisFrame && context.PreviousMode == RewindMode.Playback && context.IsRecordPhase)
            {
                _lastRecordedTick = uint.MaxValue;
            }
        }

        private void Save(ref SystemState state, ref TimeStreamWriter writer)
        {
            if (!SystemAPI.TryGetSingletonEntity<MoistureGrid>(out var gridEntity))
            {
                return;
            }

            var cells = SystemAPI.GetBuffer<MoistureGridRuntimeCell>(gridEntity);
            writer.Write(cells.Length);

            for (int i = 0; i < cells.Length; i++)
            {
                writer.Write(cells[i]);
            }

            var simulation = SystemAPI.GetSingleton<MoistureGridSimulationState>();
            writer.Write(simulation);
        }

        private void Load(ref SystemState state, ref TimeStreamReader reader)
        {
            if (!SystemAPI.TryGetSingletonEntity<MoistureGrid>(out var gridEntity))
            {
                return;
            }

            var length = reader.Read<int>();
            var buffer = SystemAPI.GetBuffer<MoistureGridRuntimeCell>(gridEntity);
            buffer.ResizeUninitialized(length);

            for (int i = 0; i < length; i++)
            {
                buffer[i] = reader.Read<MoistureGridRuntimeCell>();
            }

            var simulation = reader.Read<MoistureGridSimulationState>();
            var simRef = SystemAPI.GetSingletonRW<MoistureGridSimulationState>();
            simRef.ValueRW = simulation;
        }

        private uint GetHorizonTicks(in TimeState timeState)
        {
            if (_horizonTicks != 0)
            {
                return _horizonTicks;
            }

            float ticksPerSecond = 1f / math.max(0.0001f, timeState.FixedDeltaTime);
            uint desired = (uint)math.max(1f, math.round(ticksPerSecond * 3f));

            if (SystemAPI.TryGetSingleton<HistorySettings>(out var settings))
            {
                desired = math.max(desired, (uint)math.round(settings.DefaultTicksPerSecond * 3f));
            }

            _horizonTicks = desired + 4u;
            _history.SetMaxRecords((int)math.max(8u, _horizonTicks + 8u));
            return _horizonTicks;
        }
    }
}
