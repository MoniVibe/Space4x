using System;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Pooling
{
    internal struct EntityPoolEntry
    {
        public Entity Prefab;
        public NativeList<Entity> Available;
        public NativeList<Entity> Borrowed;
    }

    internal sealed class EntityPoolRegistry : IDisposable
    {
        private NativeHashMap<Entity, EntityPoolEntry> _pools;
        private Allocator _allocator;
        private int _defaultPrewarmCount;
        private int _maxReserve;

        public void Initialise(Allocator allocator, int defaultPrewarmCount, int maxReserve)
        {
            _allocator = allocator;
            _defaultPrewarmCount = defaultPrewarmCount;
            _maxReserve = maxReserve;
            _pools = new NativeHashMap<Entity, EntityPoolEntry>(16, allocator);
        }

        public bool IsCreated => _pools.IsCreated;

        public Entity Borrow(Entity prefab, EntityManager entityManager)
        {
            if (!_pools.TryGetValue(prefab, out var entry))
            {
                entry = CreateEntry(prefab, entityManager);
                _pools.Add(prefab, entry);
            }

            if (entry.Available.Length == 0)
            {
                var instance = entityManager.Instantiate(prefab);
                entry.Borrowed.Add(instance);
                _pools[prefab] = entry;
                return instance;
            }

            var entity = entry.Available[entry.Available.Length - 1];
            entry.Available.RemoveAt(entry.Available.Length - 1);
            entry.Borrowed.Add(entity);
            _pools[prefab] = entry;
            return entity;
        }

        public void Return(Entity prefab, Entity entity)
        {
            if (!_pools.TryGetValue(prefab, out var entry))
            {
                // Unexpected; simply destroy the entity
                entry = default;
            }
            else
            {
                for (int i = 0; i < entry.Borrowed.Length; i++)
                {
                    if (entry.Borrowed[i] == entity)
                    {
                        entry.Borrowed.RemoveAtSwapBack(i);
                        break;
                    }
                }
            }

            if (!_pools.IsCreated)
            {
                return;
            }

            if (entry.Available.Length < _maxReserve)
            {
                entry.Available.Add(entity);
                _pools[prefab] = entry;
            }
        }

        public void Dispose()
        {
            if (!_pools.IsCreated)
            {
                return;
            }

            foreach (var kvp in _pools)
            {
                if (kvp.Value.Available.IsCreated)
                {
                    kvp.Value.Available.Dispose();
                }
                if (kvp.Value.Borrowed.IsCreated)
                {
                    kvp.Value.Borrowed.Dispose();
                }
            }

            _pools.Dispose();
            _pools = default;
        }

        private EntityPoolEntry CreateEntry(Entity prefab, EntityManager entityManager)
        {
            var entry = new EntityPoolEntry
            {
                Prefab = prefab,
                Available = new NativeList<Entity>(_defaultPrewarmCount, _allocator),
                Borrowed = new NativeList<Entity>(_defaultPrewarmCount, _allocator)
            };

            for (int i = 0; i < _defaultPrewarmCount; i++)
            {
                var instance = entityManager.Instantiate(prefab);
                entry.Available.Add(instance);
            }

            return entry;
        }
    }
}
