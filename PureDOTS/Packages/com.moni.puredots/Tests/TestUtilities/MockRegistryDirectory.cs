using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.TestUtilities
{
    /// <summary>
    /// Mock registry directory for testing registry lookups without a full system graph.
    /// </summary>
    public class MockRegistryDirectory
    {
        private readonly EntityManager _entityManager;
        private readonly Entity _directoryEntity;
        private uint _version;

        public MockRegistryDirectory(EntityManager entityManager)
        {
            _entityManager = entityManager;

            // Check if directory already exists
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryDirectory>());
            if (!query.IsEmptyIgnoreFilter)
            {
                _directoryEntity = query.GetSingletonEntity();
            }
            else
            {
                _directoryEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(_directoryEntity, new RegistryDirectory
                {
                    Version = 0,
                    LastUpdateTick = 0,
                    AggregateHash = 0
                });
                entityManager.AddBuffer<RegistryDirectoryEntry>(_directoryEntity);
            }

            _version = 0;
        }

        /// <summary>
        /// The directory singleton entity.
        /// </summary>
        public Entity DirectoryEntity => _directoryEntity;

        /// <summary>
        /// Current version of the directory.
        /// </summary>
        public uint Version => _version;

        /// <summary>
        /// Registers a registry entity in the directory.
        /// </summary>
        public void RegisterRegistry(Entity registryEntity, RegistryKind kind, string label)
        {
            var buffer = _entityManager.GetBuffer<RegistryDirectoryEntry>(_directoryEntity);

            // Check if already registered
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Handle.RegistryEntity == registryEntity)
                {
                    return;
                }
            }

            buffer.Add(new RegistryDirectoryEntry
            {
                Handle = new RegistryHandle(registryEntity, kind, 0, RegistryHandleFlags.None, 1),
                Kind = kind,
                Label = new FixedString64Bytes(label)
            });

            _version++;
            UpdateDirectoryState();
        }

        /// <summary>
        /// Unregisters a registry entity from the directory.
        /// </summary>
        public void UnregisterRegistry(Entity registryEntity)
        {
            var buffer = _entityManager.GetBuffer<RegistryDirectoryEntry>(_directoryEntity);

            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Handle.RegistryEntity == registryEntity)
                {
                    buffer.RemoveAt(i);
                    _version++;
                    UpdateDirectoryState();
                    return;
                }
            }
        }

        /// <summary>
        /// Tries to get a registry handle by kind.
        /// </summary>
        public bool TryGetRegistry(RegistryKind kind, out RegistryHandle handle)
        {
            var buffer = _entityManager.GetBuffer<RegistryDirectoryEntry>(_directoryEntity);
            return buffer.TryGetHandle(kind, out handle);
        }

        /// <summary>
        /// Gets the number of registered registries.
        /// </summary>
        public int RegistryCount
        {
            get
            {
                var buffer = _entityManager.GetBuffer<RegistryDirectoryEntry>(_directoryEntity);
                return buffer.Length;
            }
        }

        /// <summary>
        /// Clears all registered registries.
        /// </summary>
        public void Clear()
        {
            var buffer = _entityManager.GetBuffer<RegistryDirectoryEntry>(_directoryEntity);
            buffer.Clear();
            _version++;
            UpdateDirectoryState();
        }

        private void UpdateDirectoryState()
        {
            var directory = _entityManager.GetComponentData<RegistryDirectory>(_directoryEntity);
            directory.Version = _version;
            _entityManager.SetComponentData(_directoryEntity, directory);
        }
    }
}

