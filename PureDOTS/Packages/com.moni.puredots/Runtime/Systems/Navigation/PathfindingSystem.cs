using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Graph-based pathfinding using A* search algorithm.
    /// Supports different locomotion modes via edge flags.
    /// Complementary to FlowFields - use NavGraph for strategic routing,
    /// FlowFields for local movement/crowds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(PathRequestSystem))]
    public partial struct PathfindingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Check budget - WARM path system
            if (!SystemAPI.HasSingleton<NavPerformanceBudget>() || !SystemAPI.HasSingleton<NavPerformanceCounters>())
            {
                return;
            }

            var budget = SystemAPI.GetSingleton<NavPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<NavPerformanceCounters>();

            // Skip if budget already exceeded
            if (counters.ValueRO.LocalPathQueriesThisTick >= budget.MaxLocalPathQueriesPerTick)
            {
                return;
            }

            // Get or create nav graph singleton
            Entity graphEntity;
            if (!SystemAPI.TryGetSingletonEntity<NavGraph>(out graphEntity))
            {
                // No graph exists yet - pathfinding not available
                return;
            }

            var graph = SystemAPI.GetComponent<NavGraph>(graphEntity);
            var nodes = SystemAPI.GetBuffer<NavNode>(graphEntity);
            var edges = SystemAPI.GetBuffer<NavEdge>(graphEntity);

            if (nodes.Length == 0 || edges.Length == 0)
            {
                return; // Empty graph
            }

            // Process WARM path requests in priority order, respecting budget
            foreach (var (request, pathState, pathResult, entity) in
                SystemAPI.Query<RefRO<PathRequest>, RefRW<PathState>, DynamicBuffer<PathResult>>()
                .WithEntityAccess())
            {
                if (request.ValueRO.IsActive == 0)
                {
                    continue;
                }

                // Only process WARM path requests (local pathfinding)
                if (request.ValueRO.HeatTier != NavHeatTier.Warm)
                {
                    continue;
                }

                // Check budget
                if (counters.ValueRO.LocalPathQueriesThisTick >= budget.MaxLocalPathQueriesPerTick)
                {
                    break; // Budget exceeded, stop processing
                }

                counters.ValueRW.LocalPathQueriesThisTick++;

                // Find nearest nodes to start and goal
                var startNode = FindNearestNode(ref nodes, request.ValueRO.StartPosition);
                var goalNode = FindNearestNode(ref nodes, request.ValueRO.GoalPosition);

                if (startNode < 0 || goalNode < 0)
                {
                    pathState.ValueRW.Status = PathStatus.Failed;
                    pathState.ValueRW.IsValid = 0;
                    continue;
                }

                // Compute path using A*
                ComputePath(
                    ref nodes,
                    ref edges,
                    startNode,
                    goalNode,
                    request.ValueRO.LocomotionMode,
                    Allocator.Temp,
                    out var path);

                // Update path result
                pathResult.Clear();
                if (path.Length > 0)
                {
                    pathState.ValueRW.Status = PathStatus.Success;
                    pathState.ValueRW.CurrentWaypointIndex = 0;
                    pathState.ValueRW.PathComputedTick = timeState.Tick;
                    pathState.ValueRW.IsValid = 1;

                    float totalCost = 0f;
                    for (int i = 0; i < path.Length; i++)
                    {
                        var nodeIndex = path[i];
                        var node = nodes[nodeIndex];
                        totalCost += node.BaseCost;

                        pathResult.Add(new PathResult
                        {
                            WaypointPosition = node.Position,
                            NodeIndex = nodeIndex,
                            CostToReach = totalCost
                        });
                    }

                    pathState.ValueRW.TotalCost = totalCost;
                }
                else
                {
                    pathState.ValueRW.Status = PathStatus.Failed;
                    pathState.ValueRW.IsValid = 0;
                }

                path.Dispose();
            }
        }

        /// <summary>
        /// Finds nearest node to a position.
        /// </summary>
        [BurstCompile]
        private static int FindNearestNode(ref DynamicBuffer<NavNode> nodes, in float3 position)
        {
            if (nodes.Length == 0)
            {
                return -1;
            }

            int nearestIndex = 0;
            float nearestDistSq = math.lengthsq(nodes[0].Position - position);

            for (int i = 1; i < nodes.Length; i++)
            {
                var distSq = math.lengthsq(nodes[i].Position - position);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        /// <summary>
        /// Computes path using A* algorithm.
        /// Phase 1: Simple A* implementation.
        /// Phase 2: Optimizations, hierarchical pathfinding, etc.
        /// </summary>
        [BurstCompile]
        private static void ComputePath(
            ref DynamicBuffer<NavNode> nodes,
            ref DynamicBuffer<NavEdge> edges,
            int startNode,
            int goalNode,
            LocomotionMode locomotionMode,
            Allocator allocator,
            out NativeList<int> path)
        {
            path = new NativeList<int>(32, allocator);

            if (startNode == goalNode)
            {
                path.Add(startNode);
                return;
            }

            // A* data structures
            var openSet = new NativeList<int>(32, allocator);
            var cameFrom = new NativeHashMap<int, int>(32, allocator);
            var gScore = new NativeHashMap<int, float>(32, allocator);
            var fScore = new NativeHashMap<int, float>(32, allocator);

            openSet.Add(startNode);
            gScore[startNode] = 0f;
            fScore[startNode] = Heuristic(nodes[startNode].Position, nodes[goalNode].Position);

            while (openSet.Length > 0)
            {
                // Find node in open set with lowest fScore
                int current = openSet[0];
                int currentIndex = 0;
                float lowestF = fScore.ContainsKey(current) ? fScore[current] : float.MaxValue;

                for (int i = 1; i < openSet.Length; i++)
                {
                    var node = openSet[i];
                    var f = fScore.ContainsKey(node) ? fScore[node] : float.MaxValue;
                    if (f < lowestF)
                    {
                        lowestF = f;
                        current = node;
                        currentIndex = i;
                    }
                }

                // Remove current from open set
                openSet.RemoveAtSwapBack(currentIndex);

                // Check if we reached goal
                if (current == goalNode)
                {
                    // Reconstruct path (builds in reverse: goal->start)
                    var reversePath = new NativeList<int>(32, Allocator.Temp);
                    reversePath.Add(current);
                    while (cameFrom.ContainsKey(current))
                    {
                        current = cameFrom[current];
                        reversePath.Add(current);
                    }
                    
                    // Copy in reverse order to get start->goal
                    for (int j = reversePath.Length - 1; j >= 0; j--)
                    {
                        path.Add(reversePath[j]);
                    }
                    
                    reversePath.Dispose();
                    break;
                }

                // Check neighbors
                var currentG = gScore.ContainsKey(current) ? gScore[current] : float.MaxValue;

                for (int i = 0; i < edges.Length; i++)
                {
                    var edge = edges[i];
                    int neighbor = -1;

                    if (edge.FromNode == current && edge.IsBidirectional != 0)
                    {
                        neighbor = edge.ToNode;
                    }
                    else if (edge.ToNode == current)
                    {
                        neighbor = edge.FromNode;
                    }
                    else if (edge.FromNode == current && edge.IsBidirectional == 0)
                    {
                        neighbor = edge.ToNode; // One-way edge
                    }

                    if (neighbor < 0)
                    {
                        continue;
                    }

                    // Check if edge allows this locomotion mode
                    if ((edge.AllowedModes & locomotionMode) == 0)
                    {
                        continue;
                    }

                    // Check if node is traversable
                    var neighborNode = nodes[neighbor];
                    if ((neighborNode.Flags & NavNodeFlags.Obstacle) != 0)
                    {
                        continue;
                    }

                    // Calculate tentative gScore
                    var tentativeG = currentG + edge.Cost + neighborNode.BaseCost;

                    var neighborG = gScore.ContainsKey(neighbor) ? gScore[neighbor] : float.MaxValue;

                    if (tentativeG < neighborG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Heuristic(neighborNode.Position, nodes[goalNode].Position);

                        // Add to open set if not already there
                        bool inOpenSet = false;
                        for (int j = 0; j < openSet.Length; j++)
                        {
                            if (openSet[j] == neighbor)
                            {
                                inOpenSet = true;
                                break;
                            }
                        }
                        if (!inOpenSet)
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }

            // Cleanup
            cameFrom.Dispose();
            gScore.Dispose();
            fScore.Dispose();
            openSet.Dispose();
        }

        /// <summary>
        /// Heuristic function for A* (Euclidean distance).
        /// </summary>
        [BurstCompile]
        private static float Heuristic(in float3 a, in float3 b)
        {
            return math.length(a - b);
        }
    }
}

