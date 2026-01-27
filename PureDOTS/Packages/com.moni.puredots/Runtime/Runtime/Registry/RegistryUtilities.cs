using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// High level categorisation for registry singletons so shared systems can query them generically.
    /// </summary>
    public enum RegistryKind : byte
    {
        Unknown = 0,
        Villager = 1,
        Resource = 2,
        Storehouse = 3,
        Miracle = 4,
        // Game-specific transport registries (MinerVessel, Hauler, Freighter, Wagon) moved to Space4X
        // Values 5-8 reserved for potential future game-specific registries
        Creature = 9,
        LogisticsRequest = 10,
        Construction = 11,
        Band = 12,
        Ability = 13,
        Spawner = 14,
        Faction = 15,
        ClimateHazard = 16,
        AreaEffect = 17,
        CultureAlignment = 18,
        ProcessingStation = 19,
        Custom = 250
    }

    /// <summary>
    /// Flags describing capabilities supported by a registry handle.
    /// </summary>
    [Flags]
    public enum RegistryHandleFlags : byte
    {
        None = 0,
        SupportsSpatialQueries = 1 << 0,
        SupportsAIQueries = 1 << 1,
        SupportsPathfinding = 1 << 2,
        Reserved = 1 << 7
    }

    /// <summary>
    /// Runtime handle that maps a registry entity to its semantic metadata.
    /// </summary>
    public struct RegistryHandle : IEquatable<RegistryHandle>
    {
        public Entity RegistryEntity;
        public RegistryKind Kind;
        public ushort ArchetypeId;
        public byte Flags;
        public uint Version;

        public RegistryHandle(Entity entity, RegistryKind kind, ushort archetypeId, RegistryHandleFlags flags, uint version)
        {
            RegistryEntity = entity;
            Kind = kind;
            ArchetypeId = archetypeId;
            Flags = (byte)flags;
            Version = version;
        }

        public readonly bool IsValid => RegistryEntity != Entity.Null;

        public readonly RegistryHandle WithVersion(uint version)
        {
            return new RegistryHandle(RegistryEntity, Kind, ArchetypeId, (RegistryHandleFlags)Flags, version);
        }

        public bool Equals(RegistryHandle other)
        {
            return RegistryEntity == other.RegistryEntity
                   && Kind == other.Kind
                   && ArchetypeId == other.ArchetypeId;
        }

        public override bool Equals(object obj)
        {
            return obj is RegistryHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)math.hash(new uint4(
                (uint)RegistryEntity.Index,
                (uint)RegistryEntity.Version,
                (uint)Kind,
                ArchetypeId)));
        }
    }

    /// <summary>
    /// Metadata component attached to registry singletons for cross-domain discovery.
    /// </summary>
    public struct RegistryMetadata : IComponentData
    {
        public RegistryKind Kind;
        public ushort ArchetypeId;
        public byte Flags;
        public int EntryCount;
        public uint Version;
        public uint LastUpdateTick;
        public RegistryId Id;
        public RegistryTelemetryKey TelemetryKey;
        public RegistryContinuityMeta ContinuityMeta;
        public RegistryContinuitySnapshot Continuity;
        public Unity.Entities.Hash128 HybridPrefabGuid;
        public FixedString64Bytes Label;

        public readonly bool SupportsSpatialQueries => ((RegistryHandleFlags)Flags & RegistryHandleFlags.SupportsSpatialQueries) != 0;

        public readonly RegistryHandle ToHandle(Entity registryEntity)
        {
            return new RegistryHandle(
                registryEntity,
                Kind,
                ArchetypeId,
                (RegistryHandleFlags)Flags,
                Version);
        }

        public void Initialise(
            RegistryKind kind,
            ushort archetypeId,
            RegistryHandleFlags flags,
            in FixedString64Bytes label,
            RegistryId id = default,
            RegistryContinuityMeta continuityMeta = default,
            RegistryTelemetryKey telemetryKey = default,
            Unity.Entities.Hash128 hybridPrefabGuid = default)
        {
            Kind = kind;
            ArchetypeId = archetypeId;
            Flags = (byte)flags;
            Label = label;
            ContinuityMeta = continuityMeta.WithDefaultsIfUnset();
            Id = id.IsValid ? id : RegistryId.FromKind(kind, label);
            TelemetryKey = telemetryKey.IsValid
                ? telemetryKey
                : RegistryTelemetryKey.FromString($"registry.{kind.ToString().ToLowerInvariant()}", Id);
            HybridPrefabGuid = hybridPrefabGuid;
            Continuity = default;
            EntryCount = 0;
            Version = 0;
            LastUpdateTick = 0;
        }

        public void MarkUpdated(int entryCount, uint tick, RegistryContinuitySnapshot continuity = default)
        {
            EntryCount = entryCount;
            LastUpdateTick = tick;
            Version++;
            Continuity = continuity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_EDITOR
            if (SupportsSpatialQueries && continuity.RequiresSpatialSync && !continuity.HasSpatialData)
            {
                throw new InvalidOperationException($"Registry '{Label}' requires spatial continuity but no spatial version was provided.");
            }
#endif
        }
    }

    /// <summary>
    /// Snapshot describing the spatial continuity state for a registry rebuild.
    /// </summary>
    public struct RegistryContinuitySnapshot
    {
        public byte SpatialDataProvided;
        public byte EnforceSpatialSync;
        public uint SpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;

        public readonly bool HasSpatialData => SpatialDataProvided != 0;
        public readonly bool RequiresSpatialSync => EnforceSpatialSync != 0;

        public static RegistryContinuitySnapshot WithSpatialData(uint spatialVersion, int resolvedCount = 0, int fallbackCount = 0, int unmappedCount = 0, bool requireSync = true)
        {
            return new RegistryContinuitySnapshot
            {
                SpatialDataProvided = 1,
                EnforceSpatialSync = requireSync ? (byte)1 : (byte)0,
                SpatialVersion = spatialVersion,
                SpatialResolvedCount = resolvedCount,
                SpatialFallbackCount = fallbackCount,
                SpatialUnmappedCount = unmappedCount
            };
        }

        public static RegistryContinuitySnapshot WithoutSpatialData(bool requireSync = false)
        {
            return new RegistryContinuitySnapshot
            {
                SpatialDataProvided = 0,
                EnforceSpatialSync = requireSync ? (byte)1 : (byte)0,
                SpatialVersion = 0,
                SpatialResolvedCount = 0,
                SpatialFallbackCount = 0,
                SpatialUnmappedCount = 0
            };
        }
    }

    /// <summary>
    /// Singleton that publishes the latest spatial grid version for registry consumers.
    /// </summary>
    public struct RegistrySpatialSyncState : IComponentData
    {
        public uint SpatialVersion;
        public uint LastPublishedTick;
        public byte HasSpatialDataFlag;

        public readonly bool HasSpatialData => HasSpatialDataFlag != 0;

        public void Publish(uint spatialVersion, uint tick)
        {
            SpatialVersion = spatialVersion;
            LastPublishedTick = tick;
            HasSpatialDataFlag = 1;
        }

        public void Reset()
        {
            SpatialVersion = 0;
            LastPublishedTick = 0;
            HasSpatialDataFlag = 0;
        }
    }

    /// <summary>
    /// Global directory tracking all active registries in the world.
    /// Updated by <see cref="PureDOTS.Systems.RegistryDirectorySystem"/> so shared systems can look up registries by kind.
    /// </summary>
    public struct RegistryDirectory : IComponentData
    {
        public uint Version;
        public uint LastUpdateTick;
        public uint AggregateHash;

        public void MarkUpdated(uint tick, uint aggregateHash)
        {
            Version++;
            LastUpdateTick = tick;
            AggregateHash = aggregateHash;
        }
    }

    /// <summary>
    /// Entry describing a registered registry singleton and the metadata required to access it.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct RegistryDirectoryEntry : IBufferElementData, IComparable<RegistryDirectoryEntry>, IEquatable<RegistryDirectoryEntry>
    {
        public RegistryHandle Handle;
        public RegistryKind Kind;
        public FixedString64Bytes Label;

        public readonly int CompareTo(RegistryDirectoryEntry other)
        {
            var kindCompare = ((int)Kind).CompareTo((int)other.Kind);
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            var lhs = Handle.RegistryEntity;
            var rhs = other.Handle.RegistryEntity;

            var indexCompare = lhs.Index.CompareTo(rhs.Index);
            if (indexCompare != 0)
            {
                return indexCompare;
            }

            return lhs.Version.CompareTo(rhs.Version);
        }

        public readonly bool Equals(RegistryDirectoryEntry other)
        {
            return Handle.Equals(other.Handle)
                   && Kind == other.Kind
                   && Label.Equals(other.Label);
        }

        public override readonly bool Equals(object obj)
        {
            return obj is RegistryDirectoryEntry other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return unchecked((int)math.hash(new uint4(
                (uint)Handle.RegistryEntity.Index,
                (uint)Handle.RegistryEntity.Version,
                (uint)Kind,
                Handle.ArchetypeId)));
        }
    }

    /// <summary>
    /// Convenience helpers for working with registry directory buffers.
    /// </summary>
    public static class RegistryDirectoryExtensions
    {
        public static bool TryGetHandle(this DynamicBuffer<RegistryDirectoryEntry> entries, RegistryKind kind, out RegistryHandle handle)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Kind == kind)
                {
                    handle = entry.Handle;
                    return true;
                }
            }

            handle = default;
            return false;
        }

        public static bool TryGetLabel(this DynamicBuffer<RegistryDirectoryEntry> entries, RegistryKind kind, out FixedString64Bytes label)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Kind == kind)
                {
                    label = entry.Label;
                    return true;
                }
            }

            label = default;
            return false;
        }
    }

    /// <summary>
    /// Helpers for resolving registry entities and buffers via the shared directory.
    /// </summary>
    public static class RegistryDirectoryLookup
    {
        public static bool TryGetRegistryEntity(ref SystemState state, RegistryKind kind, out Entity registryEntity)
        {
            registryEntity = Entity.Null;

            var entityManager = state.EntityManager;
            var directoryQuery = state.GetEntityQuery(ComponentType.ReadOnly<RegistryDirectory>());
            if (directoryQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var directoryEntity = directoryQuery.GetSingletonEntity();

            if (!entityManager.HasBuffer<RegistryDirectoryEntry>(directoryEntity))
            {
                return false;
            }

            var entries = entityManager.GetBuffer<RegistryDirectoryEntry>(directoryEntity);
            if (!entries.TryGetHandle(kind, out var handle))
            {
                return false;
            }

            if (!entityManager.Exists(handle.RegistryEntity))
            {
                return false;
            }

            registryEntity = handle.RegistryEntity;
            return true;
        }

        public static bool TryGetRegistryBuffer<TEntry>(ref SystemState state, RegistryKind kind, out DynamicBuffer<TEntry> buffer)
            where TEntry : unmanaged, IBufferElementData
        {
            buffer = default;

            if (!TryGetRegistryEntity(ref state, kind, out var entity))
            {
                return false;
            }

            var entityManager = state.EntityManager;

            if (!entityManager.HasBuffer<TEntry>(entity))
            {
                return false;
            }

            buffer = entityManager.GetBuffer<TEntry>(entity);
            return true;
        }

        public static bool TryGetRegistryMetadata(ref SystemState state, RegistryKind kind, out RegistryMetadata metadata, out Entity registryEntity)
        {
            metadata = default;

            if (!TryGetRegistryEntity(ref state, kind, out registryEntity))
            {
                return false;
            }

            var entityManager = state.EntityManager;

            if (!entityManager.HasComponent<RegistryMetadata>(registryEntity))
            {
                registryEntity = Entity.Null;
                return false;
            }

            metadata = entityManager.GetComponentData<RegistryMetadata>(registryEntity);
            return true;
        }
    }

    /// <summary>
    /// Accumulates derived statistics while writing registry data.
    /// </summary>
    /// <typeparam name="TEntry">Entry type processed by the accumulator.</typeparam>
    public interface IRegistryAccumulator<TEntry>
        where TEntry : unmanaged
    {
        void Accumulate(in TEntry entry);
    }

    /// <summary>
    /// Interface implemented by registry buffer elements that expose a canonical entity reference.
    /// </summary>
    public interface IRegistryEntry
    {
        Entity RegistryEntity { get; }
    }

    /// <summary>
    /// Interface for entries that expose a byte-sized flag field.
    /// </summary>
    public interface IRegistryFlaggedEntry
    {
        byte RegistryFlags { get; }
    }

    /// <summary>
    /// Accumulator that counts entries which match a supplied flag mask.
    /// </summary>
    /// <typeparam name="TEntry">Registry entry type.</typeparam>
    public struct RegistryFlagAccumulator<TEntry> : IRegistryAccumulator<TEntry>
        where TEntry : unmanaged, IRegistryFlaggedEntry
    {
        public byte Mask;
        public int MatchingCount;

        public void Accumulate(in TEntry entry)
        {
            if ((entry.RegistryFlags & Mask) != 0)
            {
                MatchingCount++;
            }
        }
    }

    /// <summary>
    /// Helper for building deterministic registry buffers without allocations.
    /// </summary>
    /// <typeparam name="TEntry">Entry type written into the registry buffer.</typeparam>
    public struct DeterministicRegistryBuilder<TEntry> : IDisposable
        where TEntry : unmanaged, IBufferElementData, IComparable<TEntry>
    {
        private NativeList<TEntry> _entries;

        public DeterministicRegistryBuilder(int capacity, Allocator allocator)
        {
            _entries = new NativeList<TEntry>(allocator);
            _entries.Capacity = math.max(0, capacity);
        }

        public readonly int Length => _entries.IsCreated ? _entries.Length : 0;

        public void Add(in TEntry entry)
        {
            _entries.Add(entry);
        }

        public void ApplyTo(ref DynamicBuffer<TEntry> buffer)
        {
            if (_entries.Length > 1)
            {
                var array = _entries.AsArray();
                NativeSortExtension.Sort(array);
            }

            buffer.Clear();
            buffer.ResizeUninitialized(_entries.Length);

            if (_entries.Length > 0)
            {
                buffer.AsNativeArray().CopyFrom(_entries.AsArray());
            }
        }

        public void ApplyTo<TAccumulator>(ref DynamicBuffer<TEntry> buffer, ref TAccumulator accumulator)
            where TAccumulator : struct, IRegistryAccumulator<TEntry>
        {
            ApplyTo(ref buffer);

            if (buffer.Length == 0)
            {
                return;
            }

            var array = buffer.AsNativeArray();
            for (var i = 0; i < array.Length; i++)
            {
                var entry = array[i];
                accumulator.Accumulate(in entry);
            }
        }

        public void ApplyTo(ref DynamicBuffer<TEntry> buffer, ref RegistryMetadata metadata, uint currentTick, RegistryContinuitySnapshot continuity = default)
        {
            ApplyTo(ref buffer);
            metadata.MarkUpdated(buffer.Length, currentTick, continuity);
        }

        public void ApplyTo<TAccumulator>(ref DynamicBuffer<TEntry> buffer, ref TAccumulator accumulator, ref RegistryMetadata metadata, uint currentTick, RegistryContinuitySnapshot continuity = default)
            where TAccumulator : struct, IRegistryAccumulator<TEntry>
        {
            ApplyTo(ref buffer, ref accumulator);
            metadata.MarkUpdated(buffer.Length, currentTick, continuity);
        }

        /// <summary>
        /// Applies entries to the buffer and reports to a continuity participant.
        /// </summary>
        /// <param name="buffer">Target registry buffer.</param>
        /// <param name="entityManager">Entity manager for API calls.</param>
        /// <param name="participantHandle">Handle from RegistryContinuityApi.RegisterCustomRegistry.</param>
        /// <param name="spatialVersion">Current spatial grid version.</param>
        /// <param name="currentTick">Current simulation tick.</param>
        /// <param name="resolvedCount">Number of spatially resolved entries.</param>
        /// <param name="fallbackCount">Number of fallback entries.</param>
        /// <param name="unmappedCount">Number of unmapped entries.</param>
        public void ApplyTo(
            ref DynamicBuffer<TEntry> buffer,
            EntityManager entityManager,
            in RegistryContinuityParticipantHandle participantHandle,
            uint spatialVersion,
            uint currentTick,
            int resolvedCount = 0,
            int fallbackCount = 0,
            int unmappedCount = 0)
        {
            ApplyTo(ref buffer);
            
            if (participantHandle.IsValid)
            {
                RegistryContinuityApi.ReportUpdate(
                    entityManager,
                    in participantHandle,
                    resolvedCount,
                    fallbackCount,
                    unmappedCount,
                    spatialVersion,
                    currentTick);
            }
        }

        /// <summary>
        /// Applies entries to the buffer with accumulator and reports to a continuity participant.
        /// </summary>
        public void ApplyTo<TAccumulator>(
            ref DynamicBuffer<TEntry> buffer,
            ref TAccumulator accumulator,
            EntityManager entityManager,
            in RegistryContinuityParticipantHandle participantHandle,
            uint spatialVersion,
            uint currentTick,
            int resolvedCount = 0,
            int fallbackCount = 0,
            int unmappedCount = 0)
            where TAccumulator : struct, IRegistryAccumulator<TEntry>
        {
            ApplyTo(ref buffer, ref accumulator);
            
            if (participantHandle.IsValid)
            {
                RegistryContinuityApi.ReportUpdate(
                    entityManager,
                    in participantHandle,
                    resolvedCount,
                    fallbackCount,
                    unmappedCount,
                    spatialVersion,
                    currentTick);
            }
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
            {
                _entries.Dispose();
            }
        }
    }

    /// <summary>
    /// Helper routines for locating registry entries by their backing entity.
    /// </summary>
    public static class RegistryEntryLookup
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFindEntryIndex<TEntry>(this DynamicBuffer<TEntry> entries, Entity target, out int index)
            where TEntry : unmanaged, IBufferElementData, IRegistryEntry
        {
            var array = entries.AsNativeArray();
            return TryFindEntryIndex(array, target, out index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFindEntryIndex<TEntry>(NativeArray<TEntry> entries, Entity target, out int index)
            where TEntry : struct, IRegistryEntry
        {
            var targetIndex = target.Index;
            int low = 0;
            int high = entries.Length - 1;

            while (low <= high)
            {
                var mid = (low + high) >> 1;
                var candidate = entries[mid].RegistryEntity;

                if (candidate.Index == targetIndex)
                {
                    if (candidate == target)
                    {
                        index = mid;
                        return true;
                    }

                    var left = mid - 1;
                    while (left >= low && entries[left].RegistryEntity.Index == targetIndex)
                    {
                        var check = entries[left].RegistryEntity;
                        if (check == target)
                        {
                            index = left;
                            return true;
                        }
                        left--;
                    }

                    var right = mid + 1;
                    while (right <= high && entries[right].RegistryEntity.Index == targetIndex)
                    {
                        var check = entries[right].RegistryEntity;
                        if (check == target)
                        {
                            index = right;
                            return true;
                        }
                        right++;
                    }

                    break;
                }

                if (candidate.Index < targetIndex)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            index = -1;
            return false;
        }
    }
}
