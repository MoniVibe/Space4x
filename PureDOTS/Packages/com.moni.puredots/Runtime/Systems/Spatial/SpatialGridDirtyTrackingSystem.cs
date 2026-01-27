using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Spatial
{
    /// <summary>
    /// Tracks entity additions, removals, and transform changes for the spatial grid.
    /// Produces compact dirty operations consumed by <see cref="SpatialGridBuildSystem" />.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(global::PureDOTS.Systems.SpatialSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(SpatialGridBuildSystem))]
    public partial struct SpatialGridDirtyTrackingSystem : ISystem
    {
        private EntityQuery _trackedQuery;
        private EntityQuery _newlyIndexedQuery;
        private EntityQuery _removedQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _trackedQuery = SystemAPI.QueryBuilder()
                .WithAll<SpatialIndexedTag, LocalTransform, SpatialGridResidency>()
                .Build();
            _trackedQuery.SetChangedVersionFilter(ComponentType.ReadOnly<LocalTransform>());

            _newlyIndexedQuery = SystemAPI.QueryBuilder()
                .WithAll<SpatialIndexedTag, LocalTransform>()
                .WithNone<SpatialGridResidency>()
                .Build();

            _removedQuery = SystemAPI.QueryBuilder()
                .WithAll<SpatialGridResidency>()
                .WithNone<SpatialIndexedTag>()
                .Build();

            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<SpatialGridState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var viewTick = timeState.Tick;
            if (SystemAPI.TryGetSingleton<TimeContext>(out var timeContext))
            {
                viewTick = timeContext.ViewTick;
            }
            if (timeState.IsPaused)
            {
                ClearDirtyState(ref state, viewTick);
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                ClearDirtyState(ref state, viewTick);
                return;
            }

            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
            var config = SystemAPI.GetSingleton<SpatialGridConfig>();
            if (config.CellCount <= 0 || config.CellSize <= 0f)
            {
                ClearDirtyState(ref state, timeState.Tick);
                return;
            }

            var dirtyOps = state.EntityManager.GetBuffer<SpatialGridDirtyOp>(gridEntity);
            dirtyOps.Clear();

            var currentState = SystemAPI.GetComponentRO<SpatialGridState>(gridEntity).ValueRO;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var addCount = 0;
            var updateCount = 0;
            var removeCount = 0;

            // Track newly indexed entities (Add operations).
            foreach (var (transform, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>>()
                         .WithAll<SpatialIndexedTag>()
                         .WithNone<SpatialGridResidency>()
                         .WithEntityAccess())
            {
                var position = transform.ValueRO.Position;
                SpatialHash.Quantize(position, config, out var coords);
                var cellId = SpatialHash.Flatten(in coords, in config);

                if ((uint)cellId >= (uint)config.CellCount)
                {
                    continue;
                }

                dirtyOps.Add(new SpatialGridDirtyOp
                {
                    Entity = entity,
                    Position = position,
                    OldCellId = -1,
                    NewCellId = cellId,
                    Operation = SpatialGridDirtyOpType.Add
                });

                addCount++;

                ecb.AddComponent(entity, new SpatialGridResidency
                {
                    CellId = cellId,
                    LastPosition = position,
                    Version = currentState.Version
                });
            }

            // Track transform changes on already indexed entities (Update operations).
            foreach (var (transform, residency, entity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<SpatialGridResidency>>()
                         .WithAll<SpatialIndexedTag>()
                         .WithChangeFilter<LocalTransform>()
                         .WithEntityAccess())
            {
                var position = transform.ValueRO.Position;
                var previous = residency.ValueRO;

                SpatialHash.Quantize(position, config, out var coords);
                var cellId = SpatialHash.Flatten(in coords, in config);

                if ((uint)cellId >= (uint)config.CellCount)
                {
                    dirtyOps.Add(new SpatialGridDirtyOp
                    {
                        Entity = entity,
                        Position = position,
                        OldCellId = previous.CellId,
                        NewCellId = -1,
                        Operation = SpatialGridDirtyOpType.Remove
                    });

                    removeCount++;
                    ecb.RemoveComponent<SpatialGridResidency>(entity);
                    continue;
                }

                dirtyOps.Add(new SpatialGridDirtyOp
                {
                    Entity = entity,
                    Position = position,
                    OldCellId = previous.CellId,
                    NewCellId = cellId,
                    Operation = SpatialGridDirtyOpType.Update
                });

                updateCount++;

                residency.ValueRW = new SpatialGridResidency
                {
                    CellId = cellId,
                    LastPosition = position,
                    Version = currentState.Version
                };
            }

            // Track entities that are no longer indexed (Remove operations).
            foreach (var (residency, entity) in SystemAPI
                         .Query<RefRO<SpatialGridResidency>>()
                         .WithNone<SpatialIndexedTag>()
                         .WithEntityAccess())
            {
                var previous = residency.ValueRO;

                dirtyOps.Add(new SpatialGridDirtyOp
                {
                    Entity = entity,
                    Position = previous.LastPosition,
                    OldCellId = previous.CellId,
                    NewCellId = -1,
                    Operation = SpatialGridDirtyOpType.Remove
                });

                removeCount++;
                ecb.RemoveComponent<SpatialGridResidency>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            var totalDirty = addCount + updateCount + removeCount;
            var nextState = currentState;

            if (totalDirty > 0)
            {
                nextState.DirtyVersion = currentState.DirtyVersion + 1u;
                nextState.LastDirtyTick = timeState.Tick;
                nextState.DirtyAddCount = addCount;
                nextState.DirtyUpdateCount = updateCount;
                nextState.DirtyRemoveCount = removeCount;
            }
            else
            {
                nextState.DirtyAddCount = 0;
                nextState.DirtyUpdateCount = 0;
                nextState.DirtyRemoveCount = 0;
            }

            var stateRW = SystemAPI.GetComponentRW<SpatialGridState>(gridEntity);
            stateRW.ValueRW = nextState;
        }

        private void ClearDirtyState(ref SystemState state, uint tick)
        {
            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
            if (!state.EntityManager.HasBuffer<SpatialGridDirtyOp>(gridEntity))
            {
                return;
            }

            var dirtyOps = state.EntityManager.GetBuffer<SpatialGridDirtyOp>(gridEntity);
            dirtyOps.Clear();

            var stateRW = SystemAPI.GetComponentRW<SpatialGridState>(gridEntity);
            var current = stateRW.ValueRO;
            current.LastDirtyTick = tick;
            current.DirtyAddCount = 0;
            current.DirtyUpdateCount = 0;
            current.DirtyRemoveCount = 0;
            stateRW.ValueRW = current;
        }
    }
}

