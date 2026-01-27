using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Snapshots storehouse inventory state to guarantee rewind restores deposits and withdrawals.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    public partial struct StorehouseInventoryTimeAdapterSystem : ISystem
    {
        private TimeStreamHistory _history;
        private TimeAwareController _controller;
        private uint _lastRecordedTick;
        private uint _horizonTicks;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _history = new TimeStreamHistory(1024, 256, Allocator.Persistent);
            _controller = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp | TimeAwareExecutionPhase.Playback,
                TimeAwareExecutionOptions.SkipWhenPaused);
            _lastRecordedTick = uint.MaxValue;
            _horizonTicks = 0;

            state.RequireForUpdate<StorehouseInventory>();
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
            var records = new NativeList<StorehouseInventoryRecord>(Allocator.Temp);
            foreach (var (inventory, entity) in SystemAPI.Query<RefRO<StorehouseInventory>>().WithEntityAccess())
            {
                records.Add(new StorehouseInventoryRecord
                {
                    Storehouse = entity,
                    Inventory = inventory.ValueRO
                });
            }

            writer.Write(records.Length);
            for (int i = 0; i < records.Length; i++)
            {
                writer.Write(records[i]);
            }

            records.Dispose();
        }

        private void Load(ref SystemState state, ref TimeStreamReader reader)
        {
            var count = reader.Read<int>();
            for (int i = 0; i < count; i++)
            {
                var record = reader.Read<StorehouseInventoryRecord>();
                if (!SystemAPI.Exists(record.Storehouse) ||
                    !SystemAPI.HasComponent<StorehouseInventory>(record.Storehouse))
                {
                    continue;
                }

                SystemAPI.SetComponent(record.Storehouse, record.Inventory);
            }
        }

        private struct StorehouseInventoryRecord
        {
            public Entity Storehouse;
            public StorehouseInventory Inventory;
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
            var maxRecords = (int)math.max(8u, _horizonTicks + 8u);
            _history.SetMaxRecords(maxRecords);
            return _horizonTicks;
        }
    }
}
