using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Transport.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Transport.Systems
{
    /// <summary>
    /// Advances transit along hyperway links.
    /// Handles arrivals, route progression, and booking completion.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WarpTransitSystem : ISystem
    {
        private ComponentLookup<WarpBooking> _bookingLookup;
        private ComponentLookup<HyperwayRoute> _routeLookup;
        private ComponentLookup<WarpRelayNode> _nodeLookup;
        private BufferLookup<HyperwayRouteElement> _routeBufferLookup;
        private BufferLookup<WarpRelayDocking> _dockingBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _bookingLookup = state.GetComponentLookup<WarpBooking>(false);
            _routeLookup = state.GetComponentLookup<HyperwayRoute>(false);
            _nodeLookup = state.GetComponentLookup<WarpRelayNode>(false);
            _routeBufferLookup = state.GetBufferLookup<HyperwayRouteElement>(false);
            _dockingBufferLookup = state.GetBufferLookup<WarpRelayDocking>(false);
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

            _bookingLookup.Update(ref state);
            _routeLookup.Update(ref state);
            _nodeLookup.Update(ref state);
            _routeBufferLookup.Update(ref state);
            _dockingBufferLookup.Update(ref state);

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var tick = tickTimeState.Tick;

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process bookings in transit
            foreach (var (booking, bookingEntity) in SystemAPI.Query<RefRW<WarpBooking>>()
                .WithEntityAccess())
            {
                var bookingValue = booking.ValueRO;

                if (bookingValue.State != WarpBookingState.InTransit)
                {
                    continue;
                }

                // Check if arrival time reached
                if (tick < bookingValue.ExpectedArrivalTick)
                {
                    continue;
                }

                // Get route
                if (!_routeLookup.TryGetComponent(bookingEntity, out var route) ||
                    !_routeBufferLookup.HasBuffer(bookingEntity))
                {
                    // No route, mark as failed
                    booking.ValueRW.State = WarpBookingState.Failed;
                    continue;
                }

                var routeBuffer = _routeBufferLookup[bookingEntity];
                if (route.CurrentIndex >= routeBuffer.Length)
                {
                    // Route complete
                    booking.ValueRW.State = WarpBookingState.Arrived;
                    continue;
                }

                // Get current destination node
                var currentRouteElement = routeBuffer[route.CurrentIndex];
                int destinationNodeId = currentRouteElement.NodeId;

                // Find destination node entity
                Entity destNodeEntity = Entity.Null;
                foreach (var (node, nodeEntity) in SystemAPI.Query<RefRO<WarpRelayNode>>()
                    .WithAll<WarpRelayNodeTag>()
                    .WithEntityAccess())
                {
                    if (node.ValueRO.NodeId == destinationNodeId)
                    {
                        destNodeEntity = nodeEntity;
                        break;
                    }
                }

                if (destNodeEntity == Entity.Null)
                {
                    // Destination node not found, mark as failed
                    booking.ValueRW.State = WarpBookingState.Failed;
                    continue;
                }

                // Remove from docking buffer at origin node
                // Find origin node (previous in route)
                if (route.CurrentIndex > 0)
                {
                    var originRouteElement = routeBuffer[route.CurrentIndex - 1];
                    int originNodeId = originRouteElement.NodeId;

                    foreach (var (node, nodeEntity) in SystemAPI.Query<RefRO<WarpRelayNode>>()
                        .WithAll<WarpRelayNodeTag>()
                        .WithEntityAccess())
                    {
                        if (node.ValueRO.NodeId == originNodeId && 
                            _dockingBufferLookup.HasBuffer(nodeEntity))
                        {
                            var dockingBuffer = _dockingBufferLookup[nodeEntity];
                            for (int i = dockingBuffer.Length - 1; i >= 0; i--)
                            {
                                if (dockingBuffer[i].DockedEntity == bookingValue.Traveller)
                                {
                                    dockingBuffer.RemoveAt(i);
                                    break;
                                }
                            }
                        }
                    }
                }

                // Move traveller to destination (simplified - in practice would use position components)
                // For now, just update booking

                // Check if more nodes in route
                if (route.CurrentIndex + 1 < routeBuffer.Length)
                {
                    // More nodes to visit - queue for next link
                    var routeRef = _routeLookup.GetRefRW(bookingEntity);
                    routeRef.ValueRW.CurrentIndex = route.CurrentIndex + 1;
                    booking.ValueRW.State = WarpBookingState.QueuedAtOriginNode;
                    booking.ValueRW.OriginNodeId = destinationNodeId;
                }
                else
                {
                    // Route complete - arrived at final destination
                    booking.ValueRW.State = WarpBookingState.Arrived;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

