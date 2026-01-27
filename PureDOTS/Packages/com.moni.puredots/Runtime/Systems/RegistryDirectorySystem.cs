using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Builds a deterministic directory of all active registries so shared systems can resolve handles by kind without hard references.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(ResourceRegistrySystem))]
    [UpdateAfter(typeof(StorehouseRegistrySystem))]
    [UpdateBefore(typeof(RegistryHealthSystem))]
    public partial struct RegistryDirectorySystem : ISystem
    {
        private EntityQuery _registryQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _registryQuery = SystemAPI.QueryBuilder()
                .WithAll<RegistryMetadata>()
                .Build();

            state.RequireForUpdate<RegistryDirectory>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var directoryEntity = SystemAPI.GetSingletonEntity<RegistryDirectory>();
            var directory = SystemAPI.GetComponentRW<RegistryDirectory>(directoryEntity);
            var entries = state.EntityManager.GetBuffer<RegistryDirectoryEntry>(directoryEntity);
            var timeState = SystemAPI.GetSingleton<TimeState>();

            var expected = math.max(entries.Length, _registryQuery.CalculateEntityCount());
            using var builder = new NativeList<RegistryDirectoryEntry>(expected, state.WorldUpdateAllocator);

            var aggregateHash = 2166136261u; // FNV-1a seed

            foreach (var (metadata, entity) in SystemAPI.Query<RefRO<RegistryMetadata>>().WithEntityAccess())
            {
                var kind = metadata.ValueRO.Kind;
                if (kind == RegistryKind.Unknown)
                {
                    continue;
                }

                var handle = metadata.ValueRO.ToHandle(entity);
                var entry = new RegistryDirectoryEntry
                {
                    Handle = handle,
                    Kind = kind,
                    Label = metadata.ValueRO.Label
                };

                builder.Add(entry);

                aggregateHash = HashStep(aggregateHash, (uint)kind);
                aggregateHash = HashStep(aggregateHash, (uint)entity.Index);
                aggregateHash = HashStep(aggregateHash, (uint)entity.Version);
                aggregateHash = HashStep(aggregateHash, metadata.ValueRO.Version);
            }

            if (builder.Length > 1)
            {
                var array = builder.AsArray();
                NativeSortExtension.Sort(array, new RegistryDirectoryComparer());
            }

            var shouldUpdate = directory.ValueRO.AggregateHash != aggregateHash || entries.Length != builder.Length;

            if (!shouldUpdate && builder.Length == entries.Length)
            {
                var existing = entries.AsNativeArray();
                var candidate = builder.AsArray();
                shouldUpdate = !ArraysEqual(existing, candidate);
            }

            if (!shouldUpdate)
            {
                return;
            }

            entries.Clear();
            entries.ResizeUninitialized(builder.Length);

            if (builder.Length > 0)
            {
                entries.AsNativeArray().CopyFrom(builder.AsArray());
            }

            directory.ValueRW.MarkUpdated(timeState.Tick, aggregateHash);
        }

        private static uint HashStep(uint current, uint value)
        {
            unchecked
            {
                const uint prime = 16777619u;
                return (current ^ value) * prime;
            }
        }

        private static bool ArraysEqual(NativeArray<RegistryDirectoryEntry> current, NativeArray<RegistryDirectoryEntry> candidate)
        {
            if (current.Length != candidate.Length)
            {
                return false;
            }

            for (var i = 0; i < current.Length; i++)
            {
                if (!current[i].Equals(candidate[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private struct RegistryDirectoryComparer : IComparer<RegistryDirectoryEntry>
        {
            public int Compare(RegistryDirectoryEntry x, RegistryDirectoryEntry y)
            {
                return x.CompareTo(y);
            }
        }
    }
}
