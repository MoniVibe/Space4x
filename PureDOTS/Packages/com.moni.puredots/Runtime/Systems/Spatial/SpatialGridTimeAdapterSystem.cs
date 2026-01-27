using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Spatial
{
    /// <summary>
    /// Snapshots and restores spatial grid buffers to keep playback deterministic.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    public partial struct SpatialGridTimeAdapterSystem : ISystem
    {
        private TimeStreamHistory _history;
        private TimeAwareController _controller;
        private uint _lastRecordedTick;
        private uint _horizonTicks;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _history = new TimeStreamHistory(4096, 256, Allocator.Persistent);
            _controller = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp | TimeAwareExecutionPhase.Playback,
                TimeAwareExecutionOptions.SkipWhenPaused);
            _lastRecordedTick = uint.MaxValue;
            _horizonTicks = 0;

            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<SpatialGridState>();
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
                RefreshResidency(ref state);
            }
        }

        private void Save(ref SystemState state, ref TimeStreamWriter writer)
        {
            if (!SystemAPI.TryGetSingletonEntity<SpatialGridConfig>(out var gridEntity))
            {
                return;
            }

            var gridState = SystemAPI.GetComponent<SpatialGridState>(gridEntity);
            writer.Write(gridState);

            var entries = SystemAPI.GetBuffer<SpatialGridEntry>(gridEntity);
            writer.Write(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                writer.Write(entries[i]);
            }

            var ranges = SystemAPI.GetBuffer<SpatialGridCellRange>(gridEntity);
            writer.Write(ranges.Length);
            for (int i = 0; i < ranges.Length; i++)
            {
                writer.Write(ranges[i]);
            }

            var dirtyOps = SystemAPI.GetBuffer<SpatialGridDirtyOp>(gridEntity);
            writer.Write(dirtyOps.Length);
            for (int i = 0; i < dirtyOps.Length; i++)
            {
                writer.Write(dirtyOps[i]);
            }

            var lookup = SystemAPI.GetBuffer<SpatialGridEntryLookup>(gridEntity);
            writer.Write(lookup.Length);
            for (int i = 0; i < lookup.Length; i++)
            {
                writer.Write(lookup[i]);
            }
        }

        private void Load(ref SystemState state, ref TimeStreamReader reader)
        {
            if (!SystemAPI.TryGetSingletonEntity<SpatialGridConfig>(out var gridEntity))
            {
                return;
            }

            var gridState = reader.Read<SpatialGridState>();
            SystemAPI.SetComponent(gridEntity, gridState);

            var entryCount = reader.Read<int>();
            var entries = SystemAPI.GetBuffer<SpatialGridEntry>(gridEntity);
            entries.ResizeUninitialized(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                entries[i] = reader.Read<SpatialGridEntry>();
            }

            var rangeCount = reader.Read<int>();
            var ranges = SystemAPI.GetBuffer<SpatialGridCellRange>(gridEntity);
            ranges.ResizeUninitialized(rangeCount);
            for (int i = 0; i < rangeCount; i++)
            {
                ranges[i] = reader.Read<SpatialGridCellRange>();
            }

            var dirtyCount = reader.Read<int>();
            var dirtyOps = SystemAPI.GetBuffer<SpatialGridDirtyOp>(gridEntity);
            dirtyOps.ResizeUninitialized(dirtyCount);
            for (int i = 0; i < dirtyCount; i++)
            {
                dirtyOps[i] = reader.Read<SpatialGridDirtyOp>();
            }

            var lookupCount = reader.Read<int>();
            var lookup = SystemAPI.GetBuffer<SpatialGridEntryLookup>(gridEntity);
            lookup.ResizeUninitialized(lookupCount);
            for (int i = 0; i < lookupCount; i++)
            {
                lookup[i] = reader.Read<SpatialGridEntryLookup>();
            }
        }

        private void RefreshResidency(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SpatialGridConfig>();
            if (config.CellCount <= 0 || config.CellSize <= 0f)
            {
                return;
            }

            var gridState = SystemAPI.GetSingleton<SpatialGridState>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (transform, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>>()
                         .WithAll<SpatialIndexedTag>()
                         .WithEntityAccess())
            {
                var position = transform.ValueRO.Position;
                SpatialHash.Quantize(position, config, out var coords);
                var cellId = SpatialHash.Flatten(in coords, in config);

                if ((uint)cellId >= (uint)config.CellCount)
                {
                    if (SystemAPI.HasComponent<SpatialGridResidency>(entity))
                    {
                        ecb.RemoveComponent<SpatialGridResidency>(entity);
                    }
                    continue;
                }

                var residency = new SpatialGridResidency
                {
                    CellId = cellId,
                    LastPosition = position,
                    Version = gridState.Version
                };

                if (SystemAPI.HasComponent<SpatialGridResidency>(entity))
                {
                    ecb.SetComponent(entity, residency);
                }
                else
                {
                    ecb.AddComponent(entity, residency);
                }
            }

            foreach (var (residency, entity) in SystemAPI
                         .Query<RefRO<SpatialGridResidency>>()
                         .WithNone<SpatialIndexedTag>()
                         .WithEntityAccess())
            {
                ecb.RemoveComponent<SpatialGridResidency>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
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
