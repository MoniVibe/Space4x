using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Resolves target entities referenced by villager AI into explicit world positions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    public partial struct VillagerTargetingSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<ResourceRegistryEntry> _resourceEntriesLookup;
        private BufferLookup<StorehouseRegistryEntry> _storehouseEntriesLookup;
        private EntityQuery _resourceRegistryQuery;
        private EntityQuery _storehouseRegistryQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _resourceEntriesLookup = state.GetBufferLookup<ResourceRegistryEntry>(isReadOnly: true);
            _storehouseEntriesLookup = state.GetBufferLookup<StorehouseRegistryEntry>(isReadOnly: true);

            _resourceRegistryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRegistry, ResourceRegistryEntry>()
                .Build();
            _storehouseRegistryQuery = SystemAPI.QueryBuilder()
                .WithAll<StorehouseRegistry, StorehouseRegistryEntry>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate(_resourceRegistryQuery);
            state.RequireForUpdate(_storehouseRegistryQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);

            // Initialize with empty arrays to ensure they're always valid
            NativeArray<ResourceRegistryEntry> resourceEntries = new NativeArray<ResourceRegistryEntry>(0, Allocator.TempJob);
            NativeArray<StorehouseRegistryEntry> storehouseEntries = new NativeArray<StorehouseRegistryEntry>(0, Allocator.TempJob);
            bool disposeResourceEntries = true;
            bool disposeStorehouseEntries = true;

            _resourceEntriesLookup.Update(ref state);
            _storehouseEntriesLookup.Update(ref state);

            var hasResourceEntries = !_resourceRegistryQuery.IsEmptyIgnoreFilter;
            if (hasResourceEntries)
            {
                var resourceEntity = _resourceRegistryQuery.GetSingletonEntity();
                if (_resourceEntriesLookup.TryGetBuffer(resourceEntity, out var resourceBuffer) && resourceBuffer.Length > 0)
                {
                    hasResourceEntries = true;
                    // Dispose the empty array and use the buffer's view (buffer owns the memory)
                    resourceEntries.Dispose();
                    resourceEntries = resourceBuffer.AsNativeArray();
                    disposeResourceEntries = false; // Don't dispose - buffer owns it
                }
                else
                {
                    hasResourceEntries = false;
                }
            }

            var hasStorehouseEntries = !_storehouseRegistryQuery.IsEmptyIgnoreFilter;
            if (hasStorehouseEntries)
            {
                var storehouseEntity = _storehouseRegistryQuery.GetSingletonEntity();
                if (_storehouseEntriesLookup.TryGetBuffer(storehouseEntity, out var storehouseBuffer) && storehouseBuffer.Length > 0)
                {
                    hasStorehouseEntries = true;
                    // Dispose the empty array and use the buffer's view (buffer owns the memory)
                    storehouseEntries.Dispose();
                    storehouseEntries = storehouseBuffer.AsNativeArray();
                    disposeStorehouseEntries = false; // Don't dispose - buffer owns it
                }
                else
                {
                    hasStorehouseEntries = false;
                }
            }

            var job = new ResolveTargetPositionsJob
            {
                TransformLookup = _transformLookup,
                ResourceEntries = resourceEntries,
                StorehouseEntries = storehouseEntries,
                HasResourceEntries = hasResourceEntries,
                HasStorehouseEntries = hasStorehouseEntries
            };

            var jobHandle = job.ScheduleParallel(state.Dependency);
            
            // Dispose arrays after job completes (only if we created empty ones)
            // Note: Arrays from AsNativeArray() are views owned by buffers, don't dispose those
            if (disposeResourceEntries)
            {
                jobHandle = resourceEntries.Dispose(jobHandle);
            }
            if (disposeStorehouseEntries)
            {
                jobHandle = storehouseEntries.Dispose(jobHandle);
            }
            
            state.Dependency = jobHandle;
        }

        [BurstCompile]
        public partial struct ResolveTargetPositionsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public NativeArray<ResourceRegistryEntry> ResourceEntries;
            [ReadOnly] public NativeArray<StorehouseRegistryEntry> StorehouseEntries;
            public bool HasResourceEntries;
            public bool HasStorehouseEntries;

            public void Execute(ref VillagerAIState aiState)
            {
                if (aiState.TargetEntity == Entity.Null)
                {
                    aiState.TargetPosition = float3.zero;
                    return;
                }

                if (TransformLookup.TryGetComponent(aiState.TargetEntity, out var targetTransform))
                {
                    aiState.TargetPosition = targetTransform.Position;
                    return;
                }

                if (HasResourceEntries && RegistryEntryLookup.TryFindEntryIndex(ResourceEntries, aiState.TargetEntity, out var resourceIndex))
                {
                    aiState.TargetPosition = ResourceEntries[resourceIndex].Position;
                    return;
                }

                if (HasStorehouseEntries && RegistryEntryLookup.TryFindEntryIndex(StorehouseEntries, aiState.TargetEntity, out var storehouseIndex))
                {
                    aiState.TargetPosition = StorehouseEntries[storehouseIndex].Position;
                    return;
                }

                aiState.TargetEntity = Entity.Null;
                aiState.TargetPosition = float3.zero;
            }
        }
    }
}
