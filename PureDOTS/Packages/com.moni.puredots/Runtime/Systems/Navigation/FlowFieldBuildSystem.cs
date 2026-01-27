using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Builds flow fields for navigation layers using Dijkstra-style propagation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(SpatialGridBuildSystem))]
    public partial struct FlowFieldBuildSystem : ISystem
    {
        private EntityQuery _goalQuery;
        private EntityQuery _obstacleQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _goalQuery = SystemAPI.QueryBuilder()
                .WithAll<FlowFieldGoalTag, LocalTransform>()
                .Build();

            _obstacleQuery = SystemAPI.QueryBuilder()
                .WithAll<FlowFieldObstacleTag, LocalTransform>()
                .Build();

            state.RequireForUpdate<FlowFieldConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.HasSingleton<FlowFieldConfig>())
            {
                return;
            }
            var configEntity = SystemAPI.GetSingletonEntity<FlowFieldConfig>();
            var config = SystemAPI.GetComponentRW<FlowFieldConfig>(configEntity);
            var layers = state.EntityManager.GetBuffer<FlowFieldLayer>(configEntity);

            if (layers.Length == 0)
            {
                return;
            }

            // Check terrain version changes (terraforming invalidates flow fields)
            uint currentTerrainVersion = 0;
            if (SystemAPI.TryGetSingleton<PureDOTS.Environment.TerrainVersion>(out var terrainVersion))
            {
                currentTerrainVersion = terrainVersion.Value;
                if (currentTerrainVersion != config.ValueRO.TerrainVersion)
                {
                    // Terrain changed, mark all layers dirty
                    for (int i = 0; i < layers.Length; i++)
                    {
                        var layer = layers[i];
                        layer.IsDirty = 1;
                        layers[i] = layer;
                    }
                    config.ValueRW.TerrainVersion = currentTerrainVersion;
                }
            }

            // Check if any layer needs rebuilding
            bool needsRebuild = false;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer.ShouldRebuild(timeState.Tick))
                {
                    needsRebuild = true;
                    break;
                }
            }

            if (!needsRebuild)
            {
                return;
            }

            // Process flow field requests
            if (state.EntityManager.HasBuffer<FlowFieldRequest>(configEntity))
            {
                var requests = state.EntityManager.GetBuffer<FlowFieldRequest>(configEntity);
                ProcessRequests(ref state, ref requests, timeState.Tick);
            }

            // Rebuild dirty layers
            var cellDataBuffer = state.EntityManager.GetBuffer<FlowFieldCellData>(configEntity);
            RebuildDirtyLayers(ref state, ref config.ValueRW, ref layers, ref cellDataBuffer, timeState.Tick);
        }

        [BurstCompile]
        private void ProcessRequests(ref SystemState state, ref DynamicBuffer<FlowFieldRequest> requests, uint currentTick)
        {
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                var request = requests[i];
                if (request.ValidityTick < currentTick || request.IsActive == 0)
                {
                    requests.RemoveAtSwapBack(i);
                }
            }
        }

        [BurstCompile]
        private void RebuildDirtyLayers(
            ref SystemState state,
            ref FlowFieldConfig config,
            ref DynamicBuffer<FlowFieldLayer> layers,
            ref DynamicBuffer<FlowFieldCellData> cellData,
            uint currentTick)
        {
            var gridSize = config.GridSize;
            var cellCount = config.CellCount;

            // Ensure cell data buffer is sized correctly
            if (cellData.Length < cellCount * layers.Length)
            {
                cellData.ResizeUninitialized(cellCount * layers.Length);
            }

            // Collect goals and obstacles
            var goals = new NativeList<FlowFieldGoal>(16, Allocator.Temp);
            var obstacles = new NativeList<int2>(64, Allocator.Temp);

            CollectGoals(ref state, ref goals);
            CollectObstacles(ref state, ref obstacles, config);

            // Rebuild each dirty layer
            for (int layerIdx = 0; layerIdx < layers.Length; layerIdx++)
            {
                var layer = layers[layerIdx];
                if (!layer.ShouldRebuild(currentTick))
                {
                    continue;
                }

                // Filter goals for this layer
                var layerGoals = new NativeList<FlowFieldGoal>(Allocator.Temp);
                for (int i = 0; i < goals.Length; i++)
                {
                    if (goals[i].LayerId == layer.LayerId)
                    {
                        layerGoals.Add(goals[i]);
                    }
                }

                // Build flow field for this layer
                BuildFlowFieldForLayer(
                    ref config,
                    ref layerGoals,
                    ref obstacles,
                    ref cellData,
                    layerIdx,
                    layer.LayerId,
                    gridSize,
                    cellCount);

                // Mark layer as rebuilt
                layer.LastBuildTick = currentTick;
                layer.IsDirty = 0;
                layers[layerIdx] = layer;
            }

            config.LastRebuildTick = currentTick;
            config.Version++;

            goals.Dispose();
            obstacles.Dispose();
        }

        [BurstCompile]
        private void CollectGoals(ref SystemState state, ref NativeList<FlowFieldGoal> goals)
        {
            foreach (var (goalTag, transform, entity) in SystemAPI.Query<RefRO<FlowFieldGoalTag>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                goals.Add(new FlowFieldGoal
                {
                    Entity = entity,
                    Position = new float2(transform.ValueRO.Position.xz),
                    LayerId = goalTag.ValueRO.LayerId,
                    Priority = goalTag.ValueRO.Priority
                });
            }
        }

        [BurstCompile]
        private void CollectObstacles(ref SystemState state, ref NativeList<int2> obstacles, FlowFieldConfig config)
        {
            if (!SystemAPI.TryGetSingleton<SpatialGridConfig>(out var spatialConfig))
            {
                return;
            }

            foreach (var (obstacleTag, transform) in SystemAPI.Query<RefRO<FlowFieldObstacleTag>, RefRO<LocalTransform>>())
            {
                var pos = transform.ValueRO.Position;
                var cellCoords = WorldToCell(new float2(pos.xz), config);
                obstacles.Add(cellCoords);
            }
        }

        [BurstCompile]
        private void BuildFlowFieldForLayer(
            ref FlowFieldConfig config,
            ref NativeList<FlowFieldGoal> goals,
            ref NativeList<int2> obstacles,
            ref DynamicBuffer<FlowFieldCellData> cellData,
            int layerIndex,
            ushort layerId,
            int2 gridSize,
            int cellCount)
        {
            if (goals.Length == 0)
            {
                return;
            }

            var baseIndex = layerIndex * cellCount;
            var costs = new NativeArray<float>(cellCount, Allocator.Temp);
            var directions = new NativeArray<float2>(cellCount, Allocator.Temp);

            // Initialize costs to max
            for (int i = 0; i < cellCount; i++)
            {
                costs[i] = float.MaxValue;
                directions[i] = float2.zero;
            }

            // Mark obstacles as impassable
            for (int i = 0; i < obstacles.Length; i++)
            {
                var cellIdx = CellCoordsToIndex(obstacles[i], gridSize);
                if (cellIdx >= 0 && cellIdx < cellCount)
                {
                    costs[cellIdx] = -1f; // Impassable marker
                }
            }

            // Dijkstra propagation from goals
            var queue = new NativeList<FlowFieldNode>(Allocator.Temp);
            var visited = new NativeArray<bool>(cellCount, Allocator.Temp);

            // Initialize queue with goals
            for (int i = 0; i < goals.Length; i++)
            {
                var goal = goals[i];
                var cellCoords = WorldToCell(goal.Position, config);
                var cellIdx = CellCoordsToIndex(cellCoords, gridSize);
                if (cellIdx >= 0 && cellIdx < cellCount && costs[cellIdx] >= 0f)
                {
                    costs[cellIdx] = 0f;
                    queue.Add(new FlowFieldNode { CellIndex = cellIdx, Cost = 0f });
                }
            }

            // Process queue
            while (queue.Length > 0)
            {
                // Find minimum cost node
                int minIdx = 0;
                float minCost = queue[0].Cost;
                for (int i = 1; i < queue.Length; i++)
                {
                    if (queue[i].Cost < minCost)
                    {
                        minCost = queue[i].Cost;
                        minIdx = i;
                    }
                }

                var node = queue[minIdx];
                queue.RemoveAtSwapBack(minIdx);

                if (visited[node.CellIndex])
                {
                    continue;
                }

                visited[node.CellIndex] = true;
                var coords = CellIndexToCoords(node.CellIndex, gridSize);

                // Check neighbors (4-directional)
                var neighbors = new NativeArray<int2>(4, Allocator.Temp);
                neighbors[0] = coords + new int2(1, 0);
                neighbors[1] = coords + new int2(-1, 0);
                neighbors[2] = coords + new int2(0, 1);
                neighbors[3] = coords + new int2(0, -1);

                for (int n = 0; n < 4; n++)
                {
                    var neighborCoords = neighbors[n];
                    if (neighborCoords.x < 0 || neighborCoords.x >= gridSize.x ||
                        neighborCoords.y < 0 || neighborCoords.y >= gridSize.y)
                    {
                        continue;
                    }

                    var neighborIdx = CellCoordsToIndex(neighborCoords, gridSize);
                    if (neighborIdx < 0 || neighborIdx >= cellCount || visited[neighborIdx])
                    {
                        continue;
                    }

                    if (costs[neighborIdx] < 0f) // Obstacle
                    {
                        continue;
                    }

                    var newCost = node.Cost + 1f; // Uniform cost for now
                    if (newCost < costs[neighborIdx])
                    {
                        costs[neighborIdx] = newCost;
                        var direction = math.normalize(new float2(neighborCoords - coords));
                        directions[neighborIdx] = direction;

                        queue.Add(new FlowFieldNode { CellIndex = neighborIdx, Cost = newCost });
                    }
                }

                neighbors.Dispose();
            }

            // Write results to buffer
            for (int i = 0; i < cellCount; i++)
            {
                var idx = baseIndex + i;
                if (idx < cellData.Length)
                {
                    cellData[idx] = new FlowFieldCellData
                    {
                        Direction = directions[i],
                        Cost = costs[i],
                        OccupancyFlags = costs[i] < 0f ? (byte)1 : (byte)0,
                        LayerId = (byte)layerId
                    };
                }
            }

            costs.Dispose();
            directions.Dispose();
            queue.Dispose();
            visited.Dispose();
        }

        [BurstCompile]
        private int2 WorldToCell(float2 worldPos, FlowFieldConfig config)
        {
            var localPos = worldPos - config.WorldBoundsMin;
            return new int2(
                (int)math.floor(localPos.x / config.CellSize),
                (int)math.floor(localPos.y / config.CellSize)
            );
        }

        [BurstCompile]
        private int CellCoordsToIndex(int2 coords, int2 gridSize)
        {
            return coords.y * gridSize.x + coords.x;
        }

        [BurstCompile]
        private int2 CellIndexToCoords(int index, int2 gridSize)
        {
            return new int2(index % gridSize.x, index / gridSize.x);
        }

        private struct FlowFieldGoal
        {
            public Entity Entity;
            public float2 Position;
            public ushort LayerId;
            public byte Priority;
        }

        private struct FlowFieldNode
        {
            public int CellIndex;
            public float Cost;
        }
    }
}

