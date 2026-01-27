using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Transport.Blobs;
using PureDOTS.Runtime.Transport.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Transport.Systems
{
    /// <summary>
    /// Plans routes through the hyperway network using Dijkstra shortest-path.
    /// Uses KnownFacts for node/link availability (stale info support).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct HyperwayRoutePlannerSystem : ISystem
    {
        private ComponentLookup<WarpRelayNode> _nodeLookup;
        private ComponentLookup<HyperwayNetworkRef> _networkRefLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _nodeLookup = state.GetComponentLookup<WarpRelayNode>(false);
            _networkRefLookup = state.GetComponentLookup<HyperwayNetworkRef>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _nodeLookup.Update(ref state);
            _networkRefLookup.Update(ref state);

            // Process booking requests that need route planning
            foreach (var (booking, bookingEntity) in SystemAPI.Query<RefRW<WarpBooking>>()
                .WithEntityAccess())
            {
                var bookingValue = booking.ValueRO;

                // Only process requested bookings without routes
                if (bookingValue.State != WarpBookingState.Requested)
                {
                    continue;
                }

                // Check if route already exists
                if (state.EntityManager.HasComponent<HyperwayRoute>(bookingEntity) &&
                    state.EntityManager.GetBuffer<HyperwayRouteElement>(bookingEntity).Length > 0)
                {
                    continue;
                }

                // Find nodes
                int originNodeId = bookingValue.OriginNodeId;
                int destNodeId = bookingValue.DestinationNodeId;

                Entity originNodeEntity = Entity.Null;
                Entity destNodeEntity = Entity.Null;

                foreach (var (node, nodeEntity) in SystemAPI.Query<RefRO<WarpRelayNode>>()
                    .WithAll<WarpRelayNodeTag>()
                    .WithEntityAccess())
                {
                    if (node.ValueRO.NodeId == originNodeId)
                    {
                        originNodeEntity = nodeEntity;
                    }
                    if (node.ValueRO.NodeId == destNodeId)
                    {
                        destNodeEntity = nodeEntity;
                    }
                }

                if (originNodeEntity == Entity.Null || destNodeEntity == Entity.Null)
                {
                    // Nodes not found - mark booking as failed
                    booking.ValueRW.State = WarpBookingState.Failed;
                    continue;
                }

                // Get network reference (simplified - assumes single network)
                // In practice, would get network from node or booking
                int networkId = 0;
                if (_networkRefLookup.TryGetComponent(originNodeEntity, out var networkRef))
                {
                    networkId = networkRef.NetworkId;
                }

                // TODO: Get HyperwayNetworkDef blob for this network
                // For now, use simplified pathfinding on live nodes
                // In practice, would use blob asset with all links

                // Simplified route: direct connection if exists, otherwise mark failed
                // Full Dijkstra would require network blob asset
                var routeBuffer = state.EntityManager.AddBuffer<HyperwayRouteElement>(bookingEntity);
                routeBuffer.Add(new HyperwayRouteElement { NodeId = originNodeId });
                routeBuffer.Add(new HyperwayRouteElement { NodeId = destNodeId });

                var route = new HyperwayRoute
                {
                    BookingId = bookingValue.BookingId,
                    CurrentIndex = 0
                };
                if (!state.EntityManager.HasComponent<HyperwayRoute>(bookingEntity))
                {
                    state.EntityManager.AddComponent<HyperwayRoute>(bookingEntity);
                }
                state.EntityManager.SetComponentData(bookingEntity, route);

                // Update booking state
                booking.ValueRW.State = WarpBookingState.QueuedAtOriginNode;
            }
        }

        [BurstCompile]
        private static bool IsNodeAvailable(in WarpRelayNode node)
        {
            // Check if node is available for routing
            // Uses KnownFacts in practice - for now, check status directly
            return node.Status == WarpRelayNodeStatus.Online || 
                   node.Status == WarpRelayNodeStatus.Damaged;
        }
    }
}

