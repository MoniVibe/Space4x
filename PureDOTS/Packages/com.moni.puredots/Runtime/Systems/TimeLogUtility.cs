using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace PureDOTS.Systems
{
    internal static class TimeLogUtility
    {
        public static int ExpandSecondsToTicks(int seconds)
        {
            return math.max(1, seconds * 60);
        }

        public static void AssertBudget(in TimeLogSettings settings, int commandCapacity, int snapshotCapacity)
        {
            if (settings.MemoryBudgetBytes <= 0)
            {
                return;
            }

            var estimatedBytes = commandCapacity * UnsafeUtility.SizeOf<InputCommandLogEntry>() +
                                 snapshotCapacity * UnsafeUtility.SizeOf<TickSnapshotLogEntry>();
            Assert.IsTrue(estimatedBytes <= settings.MemoryBudgetBytes,
                $"Time log budget exceeded ({estimatedBytes} > {settings.MemoryBudgetBytes}). Reduce retention seconds or increase budget.");
        }

        public static void EnsureCommandBuffer(ref DynamicBuffer<InputCommandLogEntry> buffer, ref InputCommandLogState state)
        {
            var capacity = math.max(0, state.Capacity);
            if (capacity == 0)
            {
                buffer.Clear();
                state.Count = 0;
                state.StartIndex = 0;
                return;
            }

            if (buffer.Length != capacity)
            {
                buffer.Clear();
                buffer.ResizeUninitialized(capacity);
                state.Count = 0;
                state.StartIndex = 0;
            }
        }

        public static void EnsureSnapshotBuffer(ref DynamicBuffer<TickSnapshotLogEntry> buffer, ref TickSnapshotLogState state)
        {
            var capacity = math.max(0, state.Capacity);
            if (capacity == 0)
            {
                buffer.Clear();
                state.Count = 0;
                state.StartIndex = 0;
                return;
            }

            if (buffer.Length != capacity)
            {
                buffer.Clear();
                buffer.ResizeUninitialized(capacity);
                state.Count = 0;
                state.StartIndex = 0;
            }
        }

        public static void AppendCommand(ref DynamicBuffer<InputCommandLogEntry> buffer, ref InputCommandLogState state, in InputCommandLogEntry entry)
        {
            if (state.Capacity <= 0)
            {
                return;
            }

            WriteRing(buffer, ref state.StartIndex, ref state.Count, state.Capacity, entry);
            state.LastTick = entry.Tick;
        }

        public static void AppendSnapshot(ref DynamicBuffer<TickSnapshotLogEntry> buffer, ref TickSnapshotLogState state, in TickSnapshotLogEntry entry)
        {
            if (state.Capacity <= 0)
            {
                return;
            }

            WriteRing(buffer, ref state.StartIndex, ref state.Count, state.Capacity, entry);
            state.LastTick = entry.Tick;
        }

        private static void WriteRing<T>(DynamicBuffer<T> buffer, ref int startIndex, ref int count, int capacity, in T entry)
            where T : unmanaged, IBufferElementData
        {
            if (capacity <= 0)
            {
                return;
            }

            var writeIndex = (startIndex + count) % capacity;
            if (count == capacity)
            {
                writeIndex = startIndex;
                startIndex = (startIndex + 1) % capacity;
            }
            else
            {
                count++;
                if (buffer.Length <= writeIndex)
                {
                    buffer.ResizeUninitialized(capacity);
                }
            }

            buffer[writeIndex] = entry;
        }
    }
}
