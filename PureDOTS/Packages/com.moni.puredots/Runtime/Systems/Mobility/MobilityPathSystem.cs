using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Mobility;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Mobility
{
    /// <summary>
    /// Resolves mobility path requests against the current mobility network snapshot and enqueues rendezvous/interception events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MobilityNetworkSystem))]
    public partial struct MobilityPathSystem : ISystem
    {
        private ComponentLookup<MobilityPathRequest> _requestLookup;
        private ComponentLookup<MobilityPathResult> _resultLookup;
        private BufferLookup<MobilityPathWaypoint> _pathLookup;

        private struct PathRequestEntry : System.IComparable<PathRequestEntry>
        {
            public Entity Entity;
            public uint RequestedTick;

            public int CompareTo(PathRequestEntry other)
            {
                var tickCompare = RequestedTick.CompareTo(other.RequestedTick);
                if (tickCompare != 0)
                {
                    return tickCompare;
                }

                return Entity.Index.CompareTo(other.Entity.Index);
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MobilityNetwork>();
            state.RequireForUpdate<TimeState>();

            _requestLookup = state.GetComponentLookup<MobilityPathRequest>(isReadOnly: true);
            _resultLookup = state.GetComponentLookup<MobilityPathResult>(isReadOnly: false);
            _pathLookup = state.GetBufferLookup<MobilityPathWaypoint>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out TimeState timeState))
            {
                return;
            }

            if (timeState.IsPaused || (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record))
            {
                return;
            }

            var networkEntity = SystemAPI.GetSingletonEntity<MobilityNetwork>();
            var waypoints = state.EntityManager.GetBuffer<MobilityWaypointEntry>(networkEntity);
            var highways = state.EntityManager.GetBuffer<MobilityHighwayEntry>(networkEntity);
            var gateways = state.EntityManager.GetBuffer<MobilityGatewayEntry>(networkEntity);

            var waypointExists = new NativeHashSet<int>(waypoints.Length, state.WorldUpdateAllocator);
            for (int i = 0; i < waypoints.Length; i++)
            {
                var waypoint = waypoints[i];
                if ((waypoint.Flags & (byte)WaypointFlags.Disabled) != 0)
                {
                    continue;
                }
                waypointExists.Add(waypoint.WaypointId);
            }

            var distanceById = new NativeParallelHashMap<int, float>(waypoints.Length, state.WorldUpdateAllocator);
            var previousById = new NativeParallelHashMap<int, int>(waypoints.Length, state.WorldUpdateAllocator);
            DynamicBuffer<MobilityInterceptionEvent> interceptionBuffer = default;
            if (state.EntityManager.HasBuffer<MobilityInterceptionEvent>(networkEntity))
            {
                interceptionBuffer = state.EntityManager.GetBuffer<MobilityInterceptionEvent>(networkEntity);
                interceptionBuffer.Clear();
            }

            for (int i = 0; i < waypoints.Length; i++)
            {
                var id = waypoints[i].WaypointId;
                distanceById.TryAdd(id, float.PositiveInfinity);
                previousById.TryAdd(id, -1);
            }

            var adjacency = new NativeParallelMultiHashMap<int, PathEdge>(math.max(1, (highways.Length + gateways.Length) * 2), state.WorldUpdateAllocator);
            BuildAdjacency(highways, gateways, waypointExists, adjacency);

            var requestQueue = new NativeList<PathRequestEntry>(state.WorldUpdateAllocator);
            foreach (var (pathRequest, _, entity) in SystemAPI.Query<RefRO<MobilityPathRequest>, RefRO<MobilityPathResult>>().WithEntityAccess())
            {
                requestQueue.Add(new PathRequestEntry
                {
                    Entity = entity,
                    RequestedTick = pathRequest.ValueRO.RequestedTick != 0 ? pathRequest.ValueRO.RequestedTick : timeState.Tick
                });
            }

            if (requestQueue.Length > 1)
            {
                NativeSortExtension.Sort(requestQueue.AsArray());
            }

            _requestLookup.Update(ref state);
            _resultLookup.Update(ref state);
            _pathLookup.Update(ref state);

            using var openList = new NativeList<int>(waypoints.Length, state.WorldUpdateAllocator);
            using var scratchPath = new NativeList<int>(waypoints.Length, state.WorldUpdateAllocator);

            for (int i = 0; i < requestQueue.Length; i++)
            {
                var entry = requestQueue[i];
                if (!_requestLookup.HasComponent(entry.Entity) || !_resultLookup.HasComponent(entry.Entity) || !_pathLookup.HasBuffer(entry.Entity))
                {
                    continue;
                }

                var req = _requestLookup[entry.Entity];
                var result = _resultLookup[entry.Entity];
                var path = _pathLookup[entry.Entity];

                if (!waypointExists.Contains(req.FromWaypointId) || !waypointExists.Contains(req.ToWaypointId))
                {
                    result.Status = MobilityPathStatus.Failed;
                    result.EstimatedCost = 0f;
                    result.HopCount = 0;
                    result.LastUpdateTick = timeState.Tick;
                    _resultLookup[entry.Entity] = result;
                    path.Clear();
                    continue;
                }

                var cost = SolvePath(
                    req.FromWaypointId,
                    req.ToWaypointId,
                    waypoints,
                    adjacency,
                    waypointExists,
                    distanceById,
                    previousById,
                    path,
                    openList,
                    scratchPath);

                if ((req.MaxCost > 0f && cost > req.MaxCost) || cost < 0f)
                {
                    result.Status = MobilityPathStatus.Failed;
                    result.EstimatedCost = cost;
                    result.HopCount = 0;
                    result.LastUpdateTick = timeState.Tick;
                    _resultLookup[entry.Entity] = result;
                    path.Clear();
                    continue;
                }

                result.Status = MobilityPathStatus.Assigned;
                result.EstimatedCost = cost;
                result.HopCount = path.Length;
                result.LastUpdateTick = timeState.Tick;
                _resultLookup[entry.Entity] = result;

                if (interceptionBuffer.IsCreated)
                {
                    if ((req.Flags & MobilityPathRequestFlags.BroadcastRendezvous) != 0)
                    {
                        interceptionBuffer.Add(new MobilityInterceptionEvent
                        {
                            FromWaypointId = req.FromWaypointId,
                            ToWaypointId = req.ToWaypointId,
                            Tick = timeState.Tick,
                            Type = 0
                        });
                    }

                    if ((req.Flags & MobilityPathRequestFlags.AllowInterception) != 0)
                    {
                        interceptionBuffer.Add(new MobilityInterceptionEvent
                        {
                            FromWaypointId = req.FromWaypointId,
                            ToWaypointId = req.ToWaypointId,
                            Tick = timeState.Tick,
                            Type = 1
                        });
                    }
                }
            }
        }

        private static float SolvePath(
            int fromId,
            int toId,
            DynamicBuffer<MobilityWaypointEntry> waypoints,
            NativeParallelMultiHashMap<int, PathEdge> adjacency,
            NativeHashSet<int> waypointExists,
            NativeParallelHashMap<int, float> distanceById,
            NativeParallelHashMap<int, int> previousById,
            DynamicBuffer<MobilityPathWaypoint> outputPath,
            NativeList<int> openList,
            NativeList<int> scratchPath)
        {
            openList.Clear();
            scratchPath.Clear();

            for (int i = 0; i < waypoints.Length; i++)
            {
                var waypointId = waypoints[i].WaypointId;
                distanceById[waypointId] = float.PositiveInfinity;
                previousById[waypointId] = -1;
            }

            distanceById[fromId] = 0f;
            openList.Add(fromId);

            while (openList.Length > 0)
            {
                // Pick lowest cost node in open list.
                var currentIndex = 0;
                var currentId = openList[0];
                var currentCost = distanceById[currentId];
                for (int i = 1; i < openList.Length; i++)
                {
                    var candidateId = openList[i];
                    var candidateCost = distanceById[candidateId];
                    if (candidateCost < currentCost)
                    {
                        currentCost = candidateCost;
                        currentId = candidateId;
                        currentIndex = i;
                    }
                }

                // Remove current from open list
                openList.RemoveAtSwapBack(currentIndex);

                if (currentId == toId)
                {
                    break;
                }

                if (!adjacency.TryGetFirstValue(currentId, out var edge, out var it))
                {
                    continue;
                }

                do
                {
                    if (!waypointExists.Contains(edge.To))
                    {
                        continue;
                    }

                    var tentative = currentCost + edge.Cost;
                    if (!distanceById.TryGetValue(edge.To, out var oldCost) || tentative + 0.0001f < oldCost)
                    {
                        distanceById[edge.To] = tentative;
                        previousById[edge.To] = currentId;
                        if (!ListContains(openList, edge.To))
                        {
                            openList.Add(edge.To);
                        }
                    }
                }
                while (adjacency.TryGetNextValue(out edge, ref it));
            }

            if (!distanceById.TryGetValue(toId, out var finalCost) || float.IsPositiveInfinity(finalCost))
            {
                outputPath.Clear();
                return -1f;
            }

            // Reconstruct path.
            var walker = toId;
            scratchPath.Add(walker);
            while (previousById.TryGetValue(walker, out var prev))
            {
                walker = prev;
                scratchPath.Add(walker);
                if (walker == fromId)
                {
                    break;
                }
            }

            outputPath.Clear();
            for (int i = scratchPath.Length - 1; i >= 0; i--)
            {
                outputPath.Add(new MobilityPathWaypoint { WaypointId = scratchPath[i] });
            }

            return finalCost;
        }

        private static void BuildAdjacency(
            DynamicBuffer<MobilityHighwayEntry> highways,
            DynamicBuffer<MobilityGatewayEntry> gateways,
            NativeHashSet<int> waypointExists,
            NativeParallelMultiHashMap<int, PathEdge> adjacency)
        {
            for (int i = 0; i < highways.Length; i++)
            {
                var h = highways[i];
                if ((h.Flags & (byte)HighwayFlags.Blocked) != 0)
                {
                    continue;
                }

                if (!waypointExists.Contains(h.FromWaypointId) || !waypointExists.Contains(h.ToWaypointId))
                {
                    continue;
                }

                var cost = h.Cost > 0f ? h.Cost : math.max(0.01f, h.TravelTime);
                if ((h.Flags & (byte)HighwayFlags.UnderMaintenance) != 0)
                {
                    cost *= 1.25f;
                }

                adjacency.Add(h.FromWaypointId, new PathEdge { To = h.ToWaypointId, Cost = cost });
                adjacency.Add(h.ToWaypointId, new PathEdge { To = h.FromWaypointId, Cost = cost });
            }

            for (int i = 0; i < gateways.Length; i++)
            {
                var gateway = gateways[i];
                if ((gateway.Flags & (byte)(GatewayFlags.Offline | GatewayFlags.Restricted)) != 0)
                {
                    continue;
                }

                if (!waypointExists.Contains(gateway.FromWaypointId) || !waypointExists.Contains(gateway.ToWaypointId))
                {
                    continue;
                }

                const float gatewayCost = 0.01f;
                adjacency.Add(gateway.FromWaypointId, new PathEdge { To = gateway.ToWaypointId, Cost = gatewayCost });
                adjacency.Add(gateway.ToWaypointId, new PathEdge { To = gateway.FromWaypointId, Cost = gatewayCost });
            }
        }

        private struct PathEdge
        {
            public int To;
            public float Cost;
        }

        private static bool ListContains(NativeList<int> list, int value)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
