using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// High-level multi-modal pathfinding across graphs.
    /// Builds meta-graph mixing RegionGraph + TransitGraph edges, runs A* with preference-based cost function.
    /// Compares direct route vs transport routes and outputs sequence of NavSegment.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    [UpdateAfter(typeof(NavGraphHierarchySystem))]
    // Removed invalid UpdateBefore: PathfindingSystem runs in WarmPathSystemGroup.
    public partial struct MultiModalRoutePlannerSystem : ISystem
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

            // Check budget - COLD path system
            if (!SystemAPI.HasSingleton<NavPerformanceBudget>() || !SystemAPI.HasSingleton<NavPerformanceCounters>())
            {
                return;
            }

            var budget = SystemAPI.GetSingleton<NavPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<NavPerformanceCounters>();

            // Skip if budget already exceeded
            if (counters.ValueRO.StrategicRouteQueriesThisTick >= budget.MaxStrategicRoutePlansPerTick)
            {
                return;
            }

            // Get hierarchy
            if (!SystemAPI.TryGetSingletonEntity<NavGraphHierarchy>(out var hierarchyEntity))
            {
                return;
            }

            var hierarchy = SystemAPI.GetComponent<NavGraphHierarchy>(hierarchyEntity);
            if (hierarchy.RegionGraphEntity == Entity.Null && hierarchy.TransitGraphEntity == Entity.Null)
            {
                // No graphs available - fall back to basic pathfinding
                return;
            }

            // Process path requests that need multi-modal planning (COLD path)
            foreach (var (pathRequest, navPreference, entity) in
                SystemAPI.Query<RefRO<PathRequest>, RefRO<NavPreference>>()
                .WithEntityAccess())
            {
                if (pathRequest.ValueRO.IsActive == 0)
                {
                    continue;
                }

                // Only process COLD path requests (strategic/long-range)
                if (pathRequest.ValueRO.HeatTier != NavHeatTier.Cold)
                {
                    continue;
                }

                // Check budget
                if (counters.ValueRO.StrategicRouteQueriesThisTick >= budget.MaxStrategicRoutePlansPerTick)
                {
                    break; // Budget exceeded, defer to next tick
                }

                // Check if this is a long-range request (distance threshold)
                float distance = math.length(pathRequest.ValueRO.GoalPosition - pathRequest.ValueRO.StartPosition);
                if (distance < 100f) // Short range - use basic pathfinding
                {
                    continue;
                }

                counters.ValueRW.StrategicRouteQueriesThisTick++;

                // Ensure NavPath and NavPathSegment buffer exist
                if (!SystemAPI.HasComponent<NavPath>(entity))
                {
                    state.EntityManager.AddComponent<NavPath>(entity);
                }

                if (!SystemAPI.HasBuffer<NavPathSegment>(entity))
                {
                    state.EntityManager.AddBuffer<NavPathSegment>(entity);
                }

                var navPath = SystemAPI.GetComponentRW<NavPath>(entity);
                var pathSegments = SystemAPI.GetBuffer<NavPathSegment>(entity);

                // Plan multi-modal route
                PlanMultiModalRoute(
                    ref state,
                    pathRequest.ValueRO,
                    navPreference.ValueRO,
                    hierarchy,
                    entity,
                    ref navPath.ValueRW,
                    ref pathSegments);
            }
        }

        [BurstCompile]
        private void PlanMultiModalRoute(
            ref SystemState state,
            in PathRequest request,
            in NavPreference preference,
            in NavGraphHierarchy hierarchy,
            Entity requesterEntity,
            ref NavPath navPath,
            ref DynamicBuffer<NavPathSegment> pathSegments)
        {
            // Build meta-graph nodes (regions + transit nodes)
            var metaNodes = new NativeList<MetaNode>(Allocator.Temp);
            var metaEdges = new NativeList<MetaEdge>(Allocator.Temp);

            // Add region nodes to meta-graph
            if (hierarchy.RegionGraphEntity != Entity.Null)
            {
                foreach (var (regionNode, regionEntity) in SystemAPI.Query<RefRO<RegionNode>>().WithEntityAccess())
                {
                    metaNodes.Add(new MetaNode
                    {
                        NodeId = metaNodes.Length,
                        Type = MetaNodeType.Region,
                        RegionId = regionNode.ValueRO.RegionId,
                        Position = regionNode.ValueRO.Center,
                        Entity = regionEntity
                    });
                }

                // Add region edges
                if (state.EntityManager.HasBuffer<RegionEdge>(hierarchy.RegionGraphEntity))
                {
                    var regionEdges = state.EntityManager.GetBuffer<RegionEdge>(hierarchy.RegionGraphEntity);
                    for (int i = 0; i < regionEdges.Length; i++)
                    {
                        var edge = regionEdges[i];
                        int fromMetaId = FindMetaNodeByRegionId(ref metaNodes, edge.FromRegionId);
                        int toMetaId = FindMetaNodeByRegionId(ref metaNodes, edge.ToRegionId);
                        if (fromMetaId >= 0 && toMetaId >= 0)
                        {
                            metaEdges.Add(new MetaEdge
                            {
                                FromMetaId = fromMetaId,
                                ToMetaId = toMetaId,
                                Cost = edge.Cost,
                                Kind = NavSegmentKind.MoveRegion,
                                IsBidirectional = edge.IsBidirectional
                            });
                        }
                    }
                }
            }

            // Add transit nodes to meta-graph
            if (hierarchy.TransitGraphEntity != Entity.Null)
            {
                foreach (var (transitNode, transitEntity) in SystemAPI.Query<RefRO<TransitNode>>().WithEntityAccess())
                {
                    metaNodes.Add(new MetaNode
                    {
                        NodeId = metaNodes.Length,
                        Type = MetaNodeType.Transit,
                        TransitId = transitNode.ValueRO.TransitId,
                        Position = transitNode.ValueRO.Position,
                        Entity = transitEntity
                    });
                }

                // Add transit edges
                if (state.EntityManager.HasBuffer<TransitEdge>(hierarchy.TransitGraphEntity))
                {
                    var transitEdges = state.EntityManager.GetBuffer<TransitEdge>(hierarchy.TransitGraphEntity);
                    for (int i = 0; i < transitEdges.Length; i++)
                    {
                        var edge = transitEdges[i];
                        int fromMetaId = FindMetaNodeByTransitId(ref metaNodes, edge.FromTransitId);
                        int toMetaId = FindMetaNodeByTransitId(ref metaNodes, edge.ToTransitId);
                        if (fromMetaId >= 0 && toMetaId >= 0)
                        {
                            // Calculate cost using preference profile
                            float cost = preference.CalculateCost(new NavSegment
                            {
                                Kind = NavSegmentKind.UseTransport,
                                Domain = NavDomain.Ground, // TODO: Determine from transit type
                                Transport = edge.TransportEntity,
                                EstimatedTime = edge.EstimatedTime,
                                EstimatedFuel = edge.EstimatedFuel,
                                EstimatedRisk = edge.EstimatedRisk
                            });

                            metaEdges.Add(new MetaEdge
                            {
                                FromMetaId = fromMetaId,
                                ToMetaId = toMetaId,
                                Cost = cost,
                                Kind = NavSegmentKind.UseTransport,
                                TransportEntity = edge.TransportEntity,
                                EstimatedTime = edge.EstimatedTime,
                                EstimatedFuel = edge.EstimatedFuel,
                                EstimatedRisk = edge.EstimatedRisk,
                                IsBidirectional = edge.IsBidirectional
                            });
                        }
                    }
                }
            }

            // Find start and goal nodes in meta-graph
            int startMetaId = FindNearestMetaNode(ref metaNodes, request.StartPosition);
            int goalMetaId = FindNearestMetaNode(ref metaNodes, request.GoalPosition);

            if (startMetaId < 0 || goalMetaId < 0)
            {
                return; // Cannot find nodes
            }

            // Run A* on meta-graph
            var path = new NativeList<int>(Allocator.Temp);
            ComputeMetaPath(ref metaNodes, ref metaEdges, startMetaId, goalMetaId, ref path);

            // Convert meta path to NavPath segments
            pathSegments.Clear();
            for (int i = 0; i < path.Length - 1; i++)
            {
                int fromMetaId = path[i];
                int toMetaId = path[i + 1];

                var fromNode = metaNodes[fromMetaId];
                var toNode = metaNodes[toMetaId];

                // Find edge between these nodes
                MetaEdge? edge = null;
                for (int j = 0; j < metaEdges.Length; j++)
                {
                    var e = metaEdges[j];
                    if ((e.FromMetaId == fromMetaId && e.ToMetaId == toMetaId) ||
                        (e.IsBidirectional != 0 && e.FromMetaId == toMetaId && e.ToMetaId == fromMetaId))
                    {
                        edge = e;
                        break;
                    }
                }

                if (edge.HasValue)
                {
                    var segment = new NavSegment
                    {
                        Kind = edge.Value.Kind,
                        Domain = NavDomain.Ground, // TODO: Determine from node/edge type
                        Transport = edge.Value.TransportEntity,
                        FromNodeId = fromMetaId,
                        ToNodeId = toMetaId,
                        EstimatedTime = edge.Value.EstimatedTime,
                        EstimatedFuel = edge.Value.EstimatedFuel,
                        EstimatedRisk = edge.Value.EstimatedRisk,
                        StartPosition = fromNode.Position,
                        EndPosition = toNode.Position
                    };

                    pathSegments.Add(new NavPathSegment { Segment = segment });
                }
            }

            // Update NavPath
            navPath.Owner = requesterEntity;
            navPath.CurrentSegmentIndex = 0;
            navPath.PathComputedTick = SystemAPI.GetSingleton<TimeState>().Tick;
            navPath.IsValid = 1;

            // Calculate total cost
            float totalCost = 0f;
            for (int i = 0; i < pathSegments.Length; i++)
            {
                totalCost += preference.CalculateCost(pathSegments[i].Segment);
            }
            navPath.TotalCost = totalCost;

            // Dispose native collections
            metaNodes.Dispose();
            metaEdges.Dispose();
            path.Dispose();
        }

        [BurstCompile]
        private static int FindMetaNodeByRegionId(ref NativeList<MetaNode> nodes, int regionId)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Type == MetaNodeType.Region && nodes[i].RegionId == regionId)
                {
                    return i;
                }
            }
            return -1;
        }

        [BurstCompile]
        private static int FindMetaNodeByTransitId(ref NativeList<MetaNode> nodes, int transitId)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Type == MetaNodeType.Transit && nodes[i].TransitId == transitId)
                {
                    return i;
                }
            }
            return -1;
        }

        [BurstCompile]
        private static int FindNearestMetaNode(ref NativeList<MetaNode> nodes, in float3 position)
        {
            if (nodes.Length == 0)
            {
                return -1;
            }

            int nearest = 0;
            float nearestDistSq = math.lengthsq(nodes[0].Position - position);

            for (int i = 1; i < nodes.Length; i++)
            {
                float distSq = math.lengthsq(nodes[i].Position - position);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = i;
                }
            }

            return nearest;
        }

        [BurstCompile]
        private static void ComputeMetaPath(
            ref NativeList<MetaNode> nodes,
            ref NativeList<MetaEdge> edges,
            int startId,
            int goalId,
            ref NativeList<int> path)
        {
            path.Clear();

            if (startId == goalId)
            {
                path.Add(startId);
                return;
            }

            // A* on meta-graph
            var openSet = new NativeList<int>(Allocator.Temp);
            var cameFrom = new NativeHashMap<int, int>(32, Allocator.Temp);
            var gScore = new NativeHashMap<int, float>(32, Allocator.Temp);
            var fScore = new NativeHashMap<int, float>(32, Allocator.Temp);

            openSet.Add(startId);
            gScore[startId] = 0f;
            fScore[startId] = Heuristic(nodes[startId].Position, nodes[goalId].Position);

            while (openSet.Length > 0)
            {
                // Find node with lowest fScore
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

                openSet.RemoveAtSwapBack(currentIndex);

                if (current == goalId)
                {
                    // Reconstruct path
                    var reversePath = new NativeList<int>(Allocator.Temp);
                    reversePath.Add(current);
                    while (cameFrom.ContainsKey(current))
                    {
                        current = cameFrom[current];
                        reversePath.Add(current);
                    }

                    for (int j = reversePath.Length - 1; j >= 0; j--)
                    {
                        path.Add(reversePath[j]);
                    }
                    break;
                }

                var currentG = gScore.ContainsKey(current) ? gScore[current] : float.MaxValue;

                // Check neighbors
                for (int i = 0; i < edges.Length; i++)
                {
                    var edge = edges[i];
                    int neighbor = -1;

                    if (edge.FromMetaId == current)
                    {
                        neighbor = edge.ToMetaId;
                    }
                    else if (edge.IsBidirectional != 0 && edge.ToMetaId == current)
                    {
                        neighbor = edge.FromMetaId;
                    }

                    if (neighbor < 0)
                    {
                        continue;
                    }

                    float tentativeG = currentG + edge.Cost;
                    float neighborG = gScore.ContainsKey(neighbor) ? gScore[neighbor] : float.MaxValue;

                    if (tentativeG < neighborG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Heuristic(nodes[neighbor].Position, nodes[goalId].Position);

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

            // Dispose native collections
            openSet.Dispose();
            cameFrom.Dispose();
            gScore.Dispose();
            fScore.Dispose();
        }

        [BurstCompile]
        private static float Heuristic(in float3 a, in float3 b)
        {
            return math.length(a - b);
        }

        private enum MetaNodeType : byte
        {
            Region = 0,
            Transit = 1
        }

        private struct MetaNode
        {
            public int NodeId;
            public MetaNodeType Type;
            public int RegionId;
            public int TransitId;
            public float3 Position;
            public Entity Entity;
        }

        private struct MetaEdge
        {
            public int FromMetaId;
            public int ToMetaId;
            public float Cost;
            public NavSegmentKind Kind;
            public Entity TransportEntity;
            public float EstimatedTime;
            public float EstimatedFuel;
            public float EstimatedRisk;
            public byte IsBidirectional;
        }
    }
}

