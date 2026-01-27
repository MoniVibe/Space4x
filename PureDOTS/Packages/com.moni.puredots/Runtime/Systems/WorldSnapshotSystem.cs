using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Captures global world snapshots at configured intervals during Record mode.
    /// Snapshots are stored in a ring buffer for memory-bounded rewind support.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    // Removed invalid UpdateAfter: TimeHistoryRecordSystem runs in WarmPathSystemGroup.
    public partial struct WorldSnapshotSystem : ISystem
    {
        private EntityQuery _snapshotStateQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<WorldSnapshotState>();
            _snapshotStateQuery = state.GetEntityQuery(ComponentType.ReadWrite<WorldSnapshotState>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Guard: Do not mutate history/snapshots in multiplayer modes
            if (SystemAPI.TryGetSingleton<TimeSystemFeatureFlags>(out var flags) &&
                flags.IsMultiplayerSession)
            {
                // For now, do not mutate history or snapshots in multiplayer modes.
                // When we implement MP, we can selectively allow modes like MP_SnapshotsOnly.
                return;
            }

            if (!SystemAPI.TryGetSingleton(out TimeState timeState))
            {
                return;
            }
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }
            if (_snapshotStateQuery.CalculateEntityCount() != 1)
            {
                return;
            }
            var snapshotStateHandle = SystemAPI.GetSingletonRW<WorldSnapshotState>();
            ref var snapshotState = ref snapshotStateHandle.ValueRW;

            // Only capture during Record mode
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Check if snapshots are enabled
            if (!snapshotState.IsEnabled)
            {
                return;
            }

            uint currentTick = timeState.Tick;

            // Check if it's time for a snapshot
            if (snapshotState.LastSnapshotTick != 0 &&
                currentTick - snapshotState.LastSnapshotTick < snapshotState.SnapshotIntervalTicks)
            {
                return;
            }

            // Get snapshot entity and buffers
            if (!SystemAPI.TryGetSingletonEntity<WorldSnapshotState>(out var snapshotEntity))
            {
                return;
            }
            
            if (!state.EntityManager.HasBuffer<WorldSnapshotMeta>(snapshotEntity))
            {
                state.EntityManager.AddBuffer<WorldSnapshotMeta>(snapshotEntity);
            }
            if (!state.EntityManager.HasBuffer<WorldSnapshotData>(snapshotEntity))
            {
                state.EntityManager.AddBuffer<WorldSnapshotData>(snapshotEntity);
            }

            var metaBuffer = state.EntityManager.GetBuffer<WorldSnapshotMeta>(snapshotEntity);
            var dataBuffer = state.EntityManager.GetBuffer<WorldSnapshotData>(snapshotEntity);

            // Capture the snapshot
            CaptureSnapshot(ref state, ref snapshotState, ref metaBuffer, ref dataBuffer, currentTick);

            snapshotState.LastSnapshotTick = currentTick;
        }

        private void CaptureSnapshot(ref SystemState state, ref WorldSnapshotState snapshotState,
            ref DynamicBuffer<WorldSnapshotMeta> metaBuffer, ref DynamicBuffer<WorldSnapshotData> dataBuffer,
            uint currentTick)
        {
            // Ensure meta buffer has space
            while (metaBuffer.Length < snapshotState.MaxSnapshots)
            {
                metaBuffer.Add(new WorldSnapshotMeta 
                { 
                    IsValid = false,
                    OwnerPlayerId = 0,
                    Scope = TimeControlScope.Global
                });
            }

            // Calculate byte offset for new snapshot
            int byteOffset = 0;
            if (snapshotState.CurrentSnapshotCount > 0)
            {
                // Find the end of the last valid snapshot
                for (int i = 0; i < metaBuffer.Length; i++)
                {
                    var meta = metaBuffer[i];
                    if (meta.IsValid)
                    {
                        int endOffset = meta.ByteOffset + meta.ByteLength;
                        if (endOffset > byteOffset)
                        {
                            byteOffset = endOffset;
                        }
                    }
                }
            }

            // Invalidate old snapshot at this index if it exists
            int snapshotIndex = snapshotState.NextSnapshotIndex;
            if (metaBuffer[snapshotIndex].IsValid)
            {
                var oldMeta = metaBuffer[snapshotIndex];
                snapshotState.TotalMemoryBytes -= oldMeta.ByteLength;
                snapshotState.CurrentSnapshotCount--;
            }

            // Serialize entities
            var tempBuffer = new NativeList<byte>(1024, Allocator.Temp);
            int entityCount = SerializeEntities(ref state, ref tempBuffer);

            // Check memory budget
            if (snapshotState.TotalMemoryBytes + tempBuffer.Length > snapshotState.MemoryBudgetBytes)
            {
                // Need to prune old snapshots
                PruneOldSnapshots(ref snapshotState, ref metaBuffer, ref dataBuffer, tempBuffer.Length);
            }

            // Recalculate byte offset after potential pruning
            byteOffset = 0;
            for (int i = 0; i < metaBuffer.Length; i++)
            {
                var meta = metaBuffer[i];
                if (meta.IsValid && i != snapshotIndex)
                {
                    int endOffset = meta.ByteOffset + meta.ByteLength;
                    if (endOffset > byteOffset)
                    {
                        byteOffset = endOffset;
                    }
                }
            }

            // Ensure data buffer has space
            int requiredLength = byteOffset + tempBuffer.Length;
            while (dataBuffer.Length < requiredLength)
            {
                dataBuffer.Add(new WorldSnapshotData { Value = 0 });
            }

            // Copy data to buffer
            for (int i = 0; i < tempBuffer.Length; i++)
            {
                dataBuffer[byteOffset + i] = new WorldSnapshotData { Value = tempBuffer[i] };
            }

            // Calculate FNV-1a checksum for snapshot integrity validation
            uint checksum = ComputeChecksum(tempBuffer);

            // Create metadata entry
            // Single-player: OwnerPlayerId = 0 (global), Scope = Global
            // Multiplayer: Will set OwnerPlayerId and Scope based on snapshot configuration
            var newMeta = new WorldSnapshotMeta
            {
                Tick = currentTick,
                IsValid = true,
                ByteOffset = byteOffset,
                ByteLength = tempBuffer.Length,
                CompressionType = SnapshotCompressionType.None,
                EntityCount = entityCount,
                Checksum = checksum,
                OwnerPlayerId = 0, // Global snapshot (single-player default)
                Scope = TimeControlScope.Global // Single-player uses global scope only
            };

            metaBuffer[snapshotIndex] = newMeta;
            snapshotState.CurrentSnapshotCount++;
            snapshotState.TotalMemoryBytes += tempBuffer.Length;
            snapshotState.NextSnapshotIndex = (snapshotState.NextSnapshotIndex + 1) % snapshotState.MaxSnapshots;

            tempBuffer.Dispose();
        }

        private int SerializeEntities(ref SystemState state, ref NativeList<byte> buffer)
        {
            int entityCount = 0;

            // Write header placeholder (will update at end)
            WriteInt(ref buffer, 0); // Entity count placeholder

            // Serialize entities with LocalTransform and WorldSnapshotIncludeTag
            foreach (var (transform, includeTag, entity) in SystemAPI
                .Query<RefRO<LocalTransform>, RefRO<WorldSnapshotIncludeTag>>()
                .WithEntityAccess())
            {
                // Write entity header
                WriteInt(ref buffer, entity.Index);
                WriteInt(ref buffer, entity.Version);
                
                // Write transform data
                WriteFloat3(ref buffer, transform.ValueRO.Position);
                WriteQuaternion(ref buffer, transform.ValueRO.Rotation);
                WriteFloat(ref buffer, transform.ValueRO.Scale);
                
                entityCount++;
            }

            // Also capture rewindable entities without explicit include tag but with RewindableTag
            foreach (var (transform, entity) in SystemAPI
                .Query<RefRO<LocalTransform>>()
                .WithAll<RewindableTag>()
                .WithNone<WorldSnapshotIncludeTag>()
                .WithEntityAccess())
            {
                // Write entity header
                WriteInt(ref buffer, entity.Index);
                WriteInt(ref buffer, entity.Version);
                
                // Write transform data
                WriteFloat3(ref buffer, transform.ValueRO.Position);
                WriteQuaternion(ref buffer, transform.ValueRO.Rotation);
                WriteFloat(ref buffer, transform.ValueRO.Scale);
                
                entityCount++;
            }

            // Update entity count at start
            if (buffer.Length >= 4)
            {
                unsafe
                {
                    var ptr = (int*)buffer.GetUnsafePtr();
                    *ptr = entityCount;
                }
            }

            return entityCount;
        }

        private static void PruneOldSnapshots(ref WorldSnapshotState snapshotState,
            ref DynamicBuffer<WorldSnapshotMeta> metaBuffer, ref DynamicBuffer<WorldSnapshotData> dataBuffer,
            int requiredBytes)
        {
            // Find and invalidate oldest snapshots until we have enough space
            while (snapshotState.TotalMemoryBytes + requiredBytes > snapshotState.MemoryBudgetBytes &&
                   snapshotState.CurrentSnapshotCount > 0)
            {
                // Find oldest valid snapshot
                uint oldestTick = uint.MaxValue;
                int oldestIndex = -1;

                for (int i = 0; i < metaBuffer.Length; i++)
                {
                    var meta = metaBuffer[i];
                    if (meta.IsValid && meta.Tick < oldestTick)
                    {
                        oldestTick = meta.Tick;
                        oldestIndex = i;
                    }
                }

                if (oldestIndex < 0)
                {
                    break;
                }

                // Invalidate oldest snapshot
                var oldMeta = metaBuffer[oldestIndex];
                snapshotState.TotalMemoryBytes -= oldMeta.ByteLength;
                snapshotState.CurrentSnapshotCount--;
                
                var invalidMeta = metaBuffer[oldestIndex];
                invalidMeta.IsValid = false;
                metaBuffer[oldestIndex] = invalidMeta;
            }
        }

        // Serialization helpers
        private static void WriteInt(ref NativeList<byte> buffer, int value)
        {
            unsafe
            {
                var bytes = (byte*)&value;
                for (int i = 0; i < 4; i++)
                {
                    buffer.Add(bytes[i]);
                }
            }
        }

        private static void WriteFloat(ref NativeList<byte> buffer, float value)
        {
            unsafe
            {
                var bytes = (byte*)&value;
                for (int i = 0; i < 4; i++)
                {
                    buffer.Add(bytes[i]);
                }
            }
        }

        private static void WriteFloat3(ref NativeList<byte> buffer, float3 value)
        {
            WriteFloat(ref buffer, value.x);
            WriteFloat(ref buffer, value.y);
            WriteFloat(ref buffer, value.z);
        }

        private static void WriteQuaternion(ref NativeList<byte> buffer, quaternion value)
        {
            WriteFloat(ref buffer, value.value.x);
            WriteFloat(ref buffer, value.value.y);
            WriteFloat(ref buffer, value.value.z);
            WriteFloat(ref buffer, value.value.w);
        }

        /// <summary>
        /// Computes FNV-1a checksum over snapshot data for integrity validation.
        /// Used by ScenarioRunRecorder and determinism validation systems.
        /// </summary>
        private static uint ComputeChecksum(NativeList<byte> data)
        {
            const uint FnvSeed = 2166136261u;
            const uint FnvPrime = 16777619u;

            uint hash = FnvSeed;
            unsafe
            {
                var ptr = (byte*)data.GetUnsafePtr();
                for (int i = 0; i < data.Length; i++)
                {
                    unchecked
                    {
                        hash ^= ptr[i];
                        hash *= FnvPrime;
                    }
                }
            }

            return hash;
        }
    }
}

