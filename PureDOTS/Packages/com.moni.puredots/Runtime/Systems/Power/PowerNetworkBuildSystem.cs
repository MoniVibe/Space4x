using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Power
{
    /// <summary>
    /// Builds and maintains power network graphs, assigns network refs, and precomputes routing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerSourceUpdateSystem))]
    public partial struct PowerNetworkBuildSystem : ISystem
    {
        private EntityQuery _nodeQuery;
        private EntityQuery _networkQuery;
        private ComponentLookup<PowerNetwork> _networkLookup;
        private BufferLookup<PowerEdge> _edgeBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _nodeQuery = SystemAPI.QueryBuilder()
                .WithAll<PowerNode>()
                .Build();

            _networkQuery = SystemAPI.QueryBuilder()
                .WithAll<PowerNetwork>()
                .Build();

            _networkLookup = state.GetComponentLookup<PowerNetwork>(false);
            _edgeBufferLookup = state.GetBufferLookup<PowerEdge>(false);

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

            _networkLookup.Update(ref state);
            _edgeBufferLookup.Update(ref state);

            // Group nodes by network
            var nodesByNetwork = new NativeParallelMultiHashMap<int, Entity>(0, Allocator.Temp);
            var nodeEntities = _nodeQuery.ToEntityArray(Allocator.Temp);
            var nodeComponents = _nodeQuery.ToComponentDataArray<PowerNode>(Allocator.Temp);

            for (int i = 0; i < nodeEntities.Length; i++)
            {
                var node = nodeComponents[i];
                nodesByNetwork.Add(node.Network.NetworkId, nodeEntities[i]);
            }

            // Ensure network entities exist and rebuild graphs
            var networkIds = new NativeHashSet<int>(16, Allocator.Temp);
            foreach (var node in nodeComponents)
            {
                networkIds.Add(node.Network.NetworkId);
            }

            foreach (var networkId in networkIds)
            {
                EnsureNetworkEntity(ref state, networkId, nodeComponents[0].Network.Domain);
                RebuildNetworkGraph(ref state, networkId, ref nodesByNetwork, nodeEntities, nodeComponents);
            }

            // Precompute routing for consumers
            PrecomputeRouting(ref state, nodeEntities, nodeComponents);

            nodesByNetwork.Dispose();
            nodeEntities.Dispose();
            nodeComponents.Dispose();
            networkIds.Dispose();
        }

        [BurstCompile]
        private void EnsureNetworkEntity(ref SystemState state, int networkId, PowerDomain domain)
        {
            // Check if network entity exists
            var networkEntities = _networkQuery.ToEntityArray(Allocator.Temp);
            var networkComponents = _networkQuery.ToComponentDataArray<PowerNetwork>(Allocator.Temp);

            bool exists = false;
            Entity networkEntity = Entity.Null;

            for (int i = 0; i < networkEntities.Length; i++)
            {
                if (networkComponents[i].NetworkId == networkId)
                {
                    exists = true;
                    networkEntity = networkEntities[i];
                    break;
                }
            }

            if (!exists)
            {
                // Create network entity
                networkEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(networkEntity, new PowerNetwork
                {
                    NetworkId = networkId,
                    Domain = domain
                });
                state.EntityManager.AddBuffer<PowerEdge>(networkEntity);
            }

            networkEntities.Dispose();
            networkComponents.Dispose();
        }

        [BurstCompile]
        private void RebuildNetworkGraph(ref SystemState state, int networkId, ref NativeParallelMultiHashMap<int, Entity> nodesByNetwork, NativeArray<Entity> nodeEntities, NativeArray<PowerNode> nodeComponents)
        {
            // Find network entity
            Entity networkEntity = Entity.Null;
            var networkEntities = _networkQuery.ToEntityArray(Allocator.Temp);
            var networkComponents = _networkQuery.ToComponentDataArray<PowerNetwork>(Allocator.Temp);

            for (int i = 0; i < networkEntities.Length; i++)
            {
                if (networkComponents[i].NetworkId == networkId)
                {
                    networkEntity = networkEntities[i];
                    break;
                }
            }

            if (networkEntity == Entity.Null)
                return;

            // Clear existing edges
            if (_edgeBufferLookup.HasBuffer(networkEntity))
            {
                _edgeBufferLookup[networkEntity].Clear();
            }

            // Build edges: for now, simple distance-based connections
            // In a full implementation, edges would be explicitly created by construction systems
            var edges = new NativeList<PowerEdge>(16, Allocator.Temp);

            for (int i = 0; i < nodeComponents.Length; i++)
            {
                var nodeA = nodeComponents[i];
                if (nodeA.Network.NetworkId != networkId)
                    continue;

                for (int j = i + 1; j < nodeComponents.Length; j++)
                {
                    var nodeB = nodeComponents[j];
                    if (nodeB.Network.NetworkId != networkId)
                        continue;

                    // Simple heuristic: connect nodes within 100m (for villages) or 10km (for ships)
                    var maxDistance = nodeA.Network.Domain == PowerDomain.GroundLocal ? 100f : 10000f;
                    var distance = math.distance(nodeA.WorldPosition, nodeB.WorldPosition);

                    if (distance < maxDistance)
                    {
                        edges.Add(new PowerEdge
                        {
                            FromNodeId = nodeA.NodeId,
                            ToNodeId = nodeB.NodeId,
                            Length = distance,
                            MaxThroughput = 1000f, // Default capacity
                            LossPerUnit = 0.001f,  // 0.1% per meter
                            Quality = math.min(nodeA.Quality, nodeB.Quality),
                            State = 1 // Online
                        });
                    }
                }
            }

            // Add edges to network buffer
            if (_edgeBufferLookup.HasBuffer(networkEntity))
            {
                var buffer = _edgeBufferLookup[networkEntity];
                buffer.AddRange(edges.AsArray());
            }

            edges.Dispose();
            networkEntities.Dispose();
            networkComponents.Dispose();
        }

        [BurstCompile]
        private void PrecomputeRouting(ref SystemState state, NativeArray<Entity> nodeEntities, NativeArray<PowerNode> nodeComponents)
        {
            // For each consumer node, find shortest path to nearest source
            // Simple BFS implementation
            for (int i = 0; i < nodeComponents.Length; i++)
            {
                var node = nodeComponents[i];
                if (node.Type != PowerNodeType.Consumer)
                    continue;

                var routeInfo = ComputeRouteInfo(ref state, node, nodeEntities, nodeComponents);
                
                if (state.EntityManager.HasComponent<PowerRouteInfo>(nodeEntities[i]))
                {
                    state.EntityManager.SetComponentData(nodeEntities[i], routeInfo);
                }
                else
                {
                    state.EntityManager.AddComponentData(nodeEntities[i], routeInfo);
                }
            }
        }

        [BurstCompile]
        private PowerRouteInfo ComputeRouteInfo(ref SystemState state, PowerNode consumerNode, NativeArray<Entity> nodeEntities, NativeArray<PowerNode> nodeComponents)
        {
            // Find network entity
            Entity networkEntity = Entity.Null;
            var networkEntities = _networkQuery.ToEntityArray(Allocator.Temp);
            var networkComponents = _networkQuery.ToComponentDataArray<PowerNetwork>(Allocator.Temp);

            for (int i = 0; i < networkEntities.Length; i++)
            {
                if (networkComponents[i].NetworkId == consumerNode.Network.NetworkId)
                {
                    networkEntity = networkEntities[i];
                    break;
                }
            }

            if (networkEntity == Entity.Null || !_edgeBufferLookup.HasBuffer(networkEntity))
            {
                networkEntities.Dispose();
                networkComponents.Dispose();
                return new PowerRouteInfo
                {
                    NetworkId = consumerNode.Network.NetworkId,
                    NodeId = consumerNode.NodeId,
                    PathLoss = 1f,
                    PathCapacity = 0f
                };
            }

            var edges = _edgeBufferLookup[networkEntity];

            // Build node lookup
            var nodeIdToIndex = new NativeHashMap<int, int>(nodeComponents.Length, Allocator.Temp);
            for (int i = 0; i < nodeComponents.Length; i++)
            {
                if (nodeComponents[i].Network.NetworkId == consumerNode.Network.NetworkId)
                {
                    nodeIdToIndex.Add(nodeComponents[i].NodeId, i);
                }
            }

            // BFS to find nearest source
            var visited = new NativeHashSet<int>(16, Allocator.Temp);
            var queue = new NativeQueue<(int nodeId, float pathLoss, float pathCapacity)>(Allocator.Temp);
            
            queue.Enqueue((consumerNode.NodeId, 0f, float.MaxValue));

            float bestPathLoss = 1f;
            float bestPathCapacity = 0f;
            bool foundSource = false;

            while (queue.Count > 0)
            {
                var (currentNodeId, currentPathLoss, currentPathCapacity) = queue.Dequeue();

                if (visited.Contains(currentNodeId))
                    continue;

                visited.Add(currentNodeId);

                // Check if this is a source
                if (nodeIdToIndex.TryGetValue(currentNodeId, out var nodeIndex))
                {
                    var node = nodeComponents[nodeIndex];
                    if (node.Type == PowerNodeType.Source)
                    {
                        bestPathLoss = currentPathLoss;
                        bestPathCapacity = currentPathCapacity;
                        foundSource = true;
                        break;
                    }
                }

                // Add neighbors
                for (int i = 0; i < edges.Length; i++)
                {
                    var edge = edges[i];
                    if (edge.State == 0) // Offline
                        continue;

                    int nextNodeId = -1;
                    if (edge.FromNodeId == currentNodeId)
                        nextNodeId = edge.ToNodeId;
                    else if (edge.ToNodeId == currentNodeId)
                        nextNodeId = edge.FromNodeId;

                    if (nextNodeId == -1 || visited.Contains(nextNodeId))
                        continue;

                    // Compute effective loss: 1 - exp(-LossPerUnit * Length * Quality)
                    var effectiveLoss = 1f - math.exp(-edge.LossPerUnit * edge.Length * edge.Quality);
                    var newPathLoss = currentPathLoss + effectiveLoss;
                    var newPathCapacity = math.min(currentPathCapacity, edge.MaxThroughput);

                    queue.Enqueue((nextNodeId, newPathLoss, newPathCapacity));
                }
            }

            networkEntities.Dispose();
            networkComponents.Dispose();
            nodeIdToIndex.Dispose();
            visited.Dispose();
            queue.Dispose();

            return new PowerRouteInfo
            {
                NetworkId = consumerNode.Network.NetworkId,
                NodeId = consumerNode.NodeId,
                PathLoss = foundSource ? bestPathLoss : 1f,
                PathCapacity = foundSource ? bestPathCapacity : 0f
            };
        }
    }
}

