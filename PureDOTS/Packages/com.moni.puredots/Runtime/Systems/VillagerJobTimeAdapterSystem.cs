using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    public partial struct VillagerJobTimeAdapterSystem : ISystem, ITimeAware
    {
        private TimeStreamHistory _history;
        private uint _lastSavedTick;
        private uint _horizonTicks;
        private TimeAwareController _timeAware;

        public void OnCreate(ref SystemState state)
        {
            _history = new TimeStreamHistory(4096, 512, Allocator.Persistent);
            _lastSavedTick = uint.MaxValue;
            _horizonTicks = 0;
            _timeAware = new TimeAwareController(
                TimeAwareExecutionPhase.Record | TimeAwareExecutionPhase.CatchUp | TimeAwareExecutionPhase.Playback,
                TimeAwareExecutionOptions.SkipWhenPaused);

            state.RequireForUpdate<VillagerJob>();
            state.RequireForUpdate<VillagerJobTicket>();
            state.RequireForUpdate<VillagerJobProgress>();
            state.RequireForUpdate<VillagerJobCarryItem>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnDestroy(ref SystemState state)
        {
            _history.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var horizon = GetHorizonTicks(timeState);

            if (!_timeAware.TryBegin(timeState, rewindState, out var context))
            {
                return;
            }

            if (context.IsRecordPhase)
            {
                if (context.Time.Tick != _lastSavedTick)
                {
                    uint minTick = context.Time.Tick > horizon ? context.Time.Tick - horizon : 0;
                    _history.PruneOlderThan(minTick);

                    var recordIndex = _history.BeginRecord(context.Time.Tick, out var writer);
                    Save(ref state, ref writer);
                    _history.EndRecord(recordIndex);
                    _lastSavedTick = context.Time.Tick;
                }
            }
            else if (context.IsPlaybackPhase || context.IsCatchUpPhase)
            {
                uint targetTick = context.Time.Tick;
                if (!_history.TryGet(targetTick, out var slice))
                {
                    return;
                }

                var reader = new TimeStreamReader(slice);
                Load(ref state, ref reader);
            }

            if (context.ModeChangedThisFrame)
            {
                if (context.IsPlaybackPhase)
                {
                    OnRewindStart();
                }
                else if (context.PreviousMode == RewindMode.Playback && context.IsRecordPhase)
                {
                    OnRewindEnd();
                }
            }
        }

        public void OnTick(uint tick)
        {
        }

        public void Save(ref SystemState state, ref TimeStreamWriter writer)
        {
            var records = new NativeList<VillagerJobTimeRecord>(Allocator.Temp);
            foreach (var (job, ticket, progress, carry, entity) in SystemAPI.Query<RefRO<VillagerJob>, RefRO<VillagerJobTicket>, RefRO<VillagerJobProgress>, DynamicBuffer<VillagerJobCarryItem>>().WithEntityAccess())
            {
                var carrySnapshot = new FixedList32Bytes<VillagerJobCarrySnapshot>();
                for (int i = 0; i < carry.Length && carrySnapshot.Length < carrySnapshot.Capacity; i++)
                {
                    var item = carry[i];
                    carrySnapshot.Add(new VillagerJobCarrySnapshot
                    {
                        ResourceTypeIndex = item.ResourceTypeIndex,
                        Amount = item.Amount,
                        TierId = item.TierId,
                        AverageQuality = item.AverageQuality
                    });
                }

                records.Add(new VillagerJobTimeRecord
                {
                    Villager = entity,
                    Job = job.ValueRO,
                    Ticket = ticket.ValueRO,
                    Progress = progress.ValueRO,
                    Carry = carrySnapshot
                });
            }

            writer.Write(records.Length);
            for (int i = 0; i < records.Length; i++)
            {
                writer.Write(records[i]);
            }

            records.Dispose();
        }

        public void Load(ref SystemState state, ref TimeStreamReader reader)
        {
            var count = reader.Read<int>();
            for (int i = 0; i < count; i++)
            {
                var record = reader.Read<VillagerJobTimeRecord>();
                if (!SystemAPI.Exists(record.Villager))
                {
                    continue;
                }

                if (SystemAPI.HasComponent<VillagerJob>(record.Villager))
                {
                    SystemAPI.SetComponent(record.Villager, record.Job);
                }
                if (SystemAPI.HasComponent<VillagerJobTicket>(record.Villager))
                {
                    SystemAPI.SetComponent(record.Villager, record.Ticket);
                }
                if (SystemAPI.HasComponent<VillagerJobProgress>(record.Villager))
                {
                    SystemAPI.SetComponent(record.Villager, record.Progress);
                }

                if (SystemAPI.HasBuffer<VillagerJobCarryItem>(record.Villager))
                {
                    var buffer = SystemAPI.GetBuffer<VillagerJobCarryItem>(record.Villager);
                    buffer.Clear();
                    for (int c = 0; c < record.Carry.Length; c++)
                    {
                        var snapshot = record.Carry[c];
                        buffer.Add(new VillagerJobCarryItem
                        {
                            ResourceTypeIndex = snapshot.ResourceTypeIndex,
                            Amount = snapshot.Amount,
                            TierId = snapshot.TierId,
                            AverageQuality = snapshot.AverageQuality
                        });
                    }
                }
            }
        }

        public void OnRewindStart()
        {
        }

        public void OnRewindEnd()
        {
            // Ensure latest record reflects resumed state
            _lastSavedTick = uint.MaxValue;
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
            var maxRecords = (int)math.max(1u, _horizonTicks + 8u);
            _history.SetMaxRecords(maxRecords);
            return _horizonTicks;
        }

        private struct VillagerJobCarrySnapshot
        {
            public ushort ResourceTypeIndex;
            public float Amount;
            public byte TierId;
            public ushort AverageQuality;
        }

        private struct VillagerJobTimeRecord
        {
            public Entity Villager;
            public VillagerJob Job;
            public VillagerJobTicket Ticket;
            public VillagerJobProgress Progress;
            public FixedList32Bytes<VillagerJobCarrySnapshot> Carry;
        }
    }
}
