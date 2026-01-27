using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Time
{
    public interface ITimeAware
    {
        void OnTick(uint tick);
        void Save(ref SystemState state, ref TimeStreamWriter writer);
        void Load(ref SystemState state, ref TimeStreamReader reader);
        void OnRewindStart();
        void OnRewindEnd();
    }

    public struct TimeStreamWriter
    {
        internal NativeList<byte> Buffer;

        public TimeStreamWriter(ref NativeList<byte> backingBuffer, bool clearBuffer = true)
        {
            Buffer = backingBuffer;
            if (clearBuffer)
            {
                Buffer.Clear();
            }
        }

        public void Write<T>(T value) where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            var writeIndex = Buffer.Length;
            Buffer.ResizeUninitialized(writeIndex + size);

            var temp = new NativeArray<T>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            temp[0] = value;
            var bytes = temp.Reinterpret<byte>(size);
            for (int i = 0; i < size; i++)
            {
                Buffer[writeIndex + i] = bytes[i];
            }
            temp.Dispose();
        }
    }

    public struct TimeStreamReader
    {
        private NativeArray<byte> _buffer;
        private int _offset;

        public TimeStreamReader(NativeArray<byte> buffer)
        {
            _buffer = buffer;
            _offset = 0;
        }

        public T Read<T>() where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            var bytes = _buffer.GetSubArray(_offset, size);
            var arr = bytes.Reinterpret<T>(1);
            var value = arr[0];
            _offset += size;
            return value;
        }
    }

    public struct TimeStreamRecord
    {
        public uint Tick;
        public int Offset;
        public int Length;
    }

    /// <summary>
    /// Sliding window buffer of serialized time-aware snapshots used for rewind playback.
    /// </summary>
    public struct TimeStreamHistory : System.IDisposable
    {
        private NativeList<byte> _buffer;
        private NativeList<TimeStreamRecord> _records;
        private int _maxRecords;

        public bool IsCreated => _buffer.IsCreated && _records.IsCreated;

        public TimeStreamHistory(int initialBytes, int maxRecords, Allocator allocator)
        {
            _buffer = new NativeList<byte>(initialBytes, allocator);
            _records = new NativeList<TimeStreamRecord>(maxRecords, allocator);
            _maxRecords = math.max(1, maxRecords);
        }

        public void Dispose()
        {
            if (_buffer.IsCreated)
            {
                _buffer.Dispose();
            }

            if (_records.IsCreated)
            {
                _records.Dispose();
            }
        }

        public void SetMaxRecords(int maxRecords)
        {
            _maxRecords = math.max(1, maxRecords);
        }

        public void PruneOlderThan(uint minTick)
        {
            if (!IsCreated || _records.Length == 0)
            {
                return;
            }

            if (_records[0].Tick >= minTick && _records.Length <= _maxRecords)
            {
                return;
            }

            var newRecords = new NativeList<TimeStreamRecord>(_maxRecords, Allocator.Temp);
            var newBuffer = new NativeList<byte>(_buffer.Length, Allocator.Temp);

            for (int i = 0; i < _records.Length; i++)
            {
                var record = _records[i];
                if (record.Tick < minTick)
                {
                    continue;
                }

                var slice = _buffer.AsArray().GetSubArray(record.Offset, record.Length);
                var offset = newBuffer.Length;
                newBuffer.ResizeUninitialized(offset + slice.Length);
                NativeArray<byte>.Copy(slice, 0, newBuffer.AsArray(), offset, slice.Length);

                newRecords.Add(new TimeStreamRecord
                {
                    Tick = record.Tick,
                    Offset = offset,
                    Length = record.Length
                });
            }

            _buffer.Dispose();
            _records.Dispose();

            _buffer = new NativeList<byte>(newBuffer.Length, Allocator.Persistent);
            _buffer.ResizeUninitialized(newBuffer.Length);
            NativeArray<byte>.Copy(newBuffer.AsArray(), _buffer.AsArray());

            _records = new NativeList<TimeStreamRecord>(newRecords.Length, Allocator.Persistent);
            _records.ResizeUninitialized(newRecords.Length);
            NativeArray<TimeStreamRecord>.Copy(newRecords.AsArray(), _records.AsArray());

            newBuffer.Dispose();
            newRecords.Dispose();
        }

        public int BeginRecord(uint tick, out TimeStreamWriter writer)
        {
            EnsureCapacity();

            var record = new TimeStreamRecord
            {
                Tick = tick,
                Offset = _buffer.Length,
                Length = 0
            };

            _records.Add(record);
            writer = new TimeStreamWriter(ref _buffer, clearBuffer: false);
            return _records.Length - 1;
        }

        public void EndRecord(int recordIndex)
        {
            if (!IsCreated || recordIndex < 0 || recordIndex >= _records.Length)
            {
                return;
            }

            var record = _records[recordIndex];
            record.Length = _buffer.Length - record.Offset;
            _records[recordIndex] = record;
        }

        public bool TryGet(uint tick, out NativeArray<byte> slice)
        {
            slice = default;
            if (!IsCreated || _records.Length == 0)
            {
                return false;
            }

            for (int i = _records.Length - 1; i >= 0; i--)
            {
                var record = _records[i];
                if (record.Tick > tick)
                {
                    continue;
                }

                slice = _buffer.AsArray().GetSubArray(record.Offset, record.Length);
                return true;
            }

            return false;
        }

        private void EnsureCapacity()
        {
            if (_records.Length < _maxRecords)
            {
                return;
            }

            if (_records.Length == 0)
            {
                _buffer.Clear();
                return;
            }

            var oldestTick = _records[0].Tick;
            PruneOlderThan(oldestTick + 1);
        }
    }

    [System.Flags]
    public enum TimeAwareExecutionPhase : byte
    {
        None = 0,
        Record = 1 << 0,
        CatchUp = 1 << 1,
        Playback = 1 << 2
    }

    [System.Flags]
    public enum TimeAwareExecutionOptions : byte
    {
        None = 0,
        SkipWhenPaused = 1 << 0
    }

    public struct TimeAwareContext
    {
        public TimeState Time;
        public RewindState Rewind;
        public TimeAwareExecutionPhase Phase;
        public bool ModeChangedThisFrame;
        public RewindMode PreviousMode;

        public readonly bool IsRecordPhase => Phase == TimeAwareExecutionPhase.Record;
        public readonly bool IsCatchUpPhase => Phase == TimeAwareExecutionPhase.CatchUp;
        public readonly bool IsPlaybackPhase => Phase == TimeAwareExecutionPhase.Playback;
    }

    public struct TimeAwareController
    {
        private readonly TimeAwareExecutionPhase _phases;
        private readonly TimeAwareExecutionOptions _options;
        private RewindMode _lastMode;
        private bool _initialised;

        public TimeAwareController(TimeAwareExecutionPhase phases, TimeAwareExecutionOptions options = TimeAwareExecutionOptions.None)
        {
            _phases = phases;
            _options = options;
            _lastMode = RewindMode.Record;
            _initialised = false;
        }

        public bool TryBegin(in TimeState timeState, in RewindState rewindState, out TimeAwareContext context)
        {
            context = default;

            var phase = ConvertModeToPhase(rewindState.Mode);
            if ((_phases & phase) == 0)
            {
                UpdateModeCache(rewindState.Mode);
                return false;
            }

            if ((_options & TimeAwareExecutionOptions.SkipWhenPaused) != 0 &&
                phase == TimeAwareExecutionPhase.Record &&
                timeState.IsPaused)
            {
                UpdateModeCache(rewindState.Mode);
                return false;
            }

            var previous = _lastMode;
            bool modeChanged = !_initialised || rewindState.Mode != _lastMode;
            UpdateModeCache(rewindState.Mode);

            context = new TimeAwareContext
            {
                Time = timeState,
                Rewind = rewindState,
                Phase = phase,
                ModeChangedThisFrame = modeChanged,
                PreviousMode = previous
            };

            return true;
        }

        public void Reset()
        {
            _initialised = false;
            _lastMode = RewindMode.Record;
        }

        private void UpdateModeCache(RewindMode mode)
        {
            _lastMode = mode;
            _initialised = true;
        }

        private static TimeAwareExecutionPhase ConvertModeToPhase(RewindMode mode)
        {
            return mode switch
            {
                RewindMode.Play => TimeAwareExecutionPhase.Record,
                RewindMode.Paused => TimeAwareExecutionPhase.None,
                RewindMode.Rewind => TimeAwareExecutionPhase.Playback,
                RewindMode.Step => TimeAwareExecutionPhase.Record,
                _ => TimeAwareExecutionPhase.None
            };
        }
    }

    public static class TimeAwareUtility
    {
        private struct EntityComparer : IComparer<Entity>
        {
            public int Compare(Entity x, Entity y)
            {
                var indexCompare = x.Index.CompareTo(y.Index);
                return indexCompare != 0 ? indexCompare : x.Version.CompareTo(y.Version);
            }
        }

        public static void SortEntities(NativeArray<Entity> entities)
        {
            NativeSortExtension.Sort(entities, new EntityComparer());
        }
    }
}
