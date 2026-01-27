using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Spatial
{
    /// <summary>
    /// Performs the first deterministic spatial grid rebuild during initialization.
    /// Ensures simulation starts with an up-to-date index before gameplay systems execute.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SpatialGridInitialBuildSystem : ISystem
    {
        private EntityQuery _indexedQuery;
        private EntityQuery _directoryQuery;
        private BufferLookup<RegistryDirectoryEntry> _directoryEntriesLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _indexedQuery = SystemAPI.QueryBuilder()
                .WithAll<SpatialIndexedTag, LocalTransform>()
                .Build();

            _directoryQuery = state.GetEntityQuery(ComponentType.ReadOnly<RegistryDirectory>());
            _directoryEntriesLookup = state.GetBufferLookup<RegistryDirectoryEntry>(isReadOnly: true);

            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<SpatialGridState>();
            state.RequireForUpdate(_directoryQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SpatialGridConfig>();
            if (config.CellCount <= 0 || config.CellSize <= 0f)
            {
                state.Enabled = false;
                return;
            }

            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
            var stateRW = SystemAPI.GetComponentRW<SpatialGridState>(gridEntity);
            var activeEntries = state.EntityManager.GetBuffer<SpatialGridEntry>(gridEntity);
            var activeRanges = state.EntityManager.GetBuffer<SpatialGridCellRange>(gridEntity);
            var stagingEntries = state.EntityManager.GetBuffer<SpatialGridStagingEntry>(gridEntity);
            var stagingRanges = state.EntityManager.GetBuffer<SpatialGridStagingCellRange>(gridEntity);

            stagingEntries.Clear();
            stagingRanges.Clear();

            var indexedCount = _indexedQuery.CalculateEntityCount();
            stagingEntries.EnsureCapacity(indexedCount);

            var gatherList = new NativeList<SpatialGridStagingEntry>(Allocator.Temp);
            gatherList.Capacity = math.max(gatherList.Capacity, indexedCount);

            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<SpatialIndexedTag>()
                         .WithEntityAccess())
            {
                var position = transform.ValueRO.Position;
                SpatialHash.Quantize(position, config, out var coords);
                var cellId = SpatialHash.Flatten(in coords, in config);

                if ((uint)cellId >= (uint)config.CellCount)
                {
                    continue;
                }

                gatherList.Add(new SpatialGridStagingEntry
                {
                    Entity = entity,
                    Position = position,
                    CellId = cellId
                });
            }

            gatherList.Sort(new SpatialGridBuildSystem.SpatialGridEntryCellComparer());

            var tempRanges = new NativeArray<SpatialGridStagingCellRange>(config.CellCount, Allocator.Temp, NativeArrayOptions.ClearMemory);

            var currentCell = -1;
            var cellStart = 0;

            for (var i = 0; i < gatherList.Length; i++)
            {
                var entry = gatherList[i];
                if (entry.CellId != currentCell)
                {
                    if (currentCell >= 0)
                    {
                        tempRanges[currentCell] = new SpatialGridStagingCellRange
                        {
                            StartIndex = cellStart,
                            Count = i - cellStart
                        };
                    }

                    currentCell = entry.CellId;
                    cellStart = i;
                }

                stagingEntries.Add(entry);
            }

            if (currentCell >= 0)
            {
                tempRanges[currentCell] = new SpatialGridStagingCellRange
                {
                    StartIndex = cellStart,
                    Count = gatherList.Length - cellStart
                };
            }

            stagingRanges.ResizeUninitialized(config.CellCount);
            for (var i = 0; i < config.CellCount; i++)
            {
                stagingRanges[i] = tempRanges[i];
            }

            SpatialGridBuildSystem.CopyStagingToActive(ref activeRanges, ref activeEntries, in stagingRanges, in stagingEntries);

            var lastTick = SystemAPI.HasSingleton<TimeState>() ? SystemAPI.GetSingleton<TimeState>().Tick : 0u;
            var previousState = stateRW.ValueRO;
            stateRW.ValueRW = new SpatialGridState
            {
                ActiveBufferIndex = previousState.ActiveBufferIndex,
                TotalEntries = gatherList.Length,
                Version = previousState.Version + 1,
                LastUpdateTick = lastTick,
                LastDirtyTick = previousState.LastDirtyTick,
                DirtyVersion = previousState.DirtyVersion,
                DirtyAddCount = previousState.DirtyAddCount,
                DirtyUpdateCount = previousState.DirtyUpdateCount,
                DirtyRemoveCount = previousState.DirtyRemoveCount,
                LastRebuildMilliseconds = previousState.LastRebuildMilliseconds,
                LastStrategy = previousState.LastStrategy
            };

            if (SystemAPI.HasComponent<SpatialRegistryMetadata>(gridEntity))
            {
                var metadata = SystemAPI.GetComponentRW<SpatialRegistryMetadata>(gridEntity);
                var value = metadata.ValueRO;
                value.ResetHandles();

                _directoryEntriesLookup.Update(ref state);

                if (!_directoryQuery.IsEmptyIgnoreFilter)
                {
                    var directoryEntity = _directoryQuery.GetSingletonEntity();
                    if (_directoryEntriesLookup.TryGetBuffer(directoryEntity, out var directoryEntries))
                    {
                        for (var i = 0; i < directoryEntries.Length; i++)
                        {
                            value.SetHandle(directoryEntries[i].Handle);
                        }
                    }
                }

                metadata.ValueRW = value;
            }

            gatherList.Dispose();
            tempRanges.Dispose();

            // Only need the initial build once during initialization.
            state.Enabled = false;
        }

    }
}
