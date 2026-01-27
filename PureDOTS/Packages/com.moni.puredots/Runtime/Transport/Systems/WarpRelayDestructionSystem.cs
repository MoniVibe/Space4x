using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Transport.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Systems
{
    /// <summary>
    /// Handles warp relay node destruction.
    /// Marks bookings in transit as failed, generates InfoPackets if possible.
    /// Handles remote reactions (no arrivals, no responses).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WarpRelayDestructionSystem : ISystem
    {
        private ComponentLookup<WarpRelayNode> _nodeLookup;
        private ComponentLookup<WarpBooking> _bookingLookup;
        private BufferLookup<WarpRelayDocking> _dockingBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _nodeLookup = state.GetComponentLookup<WarpRelayNode>(false);
            _bookingLookup = state.GetComponentLookup<WarpBooking>(false);
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

            _nodeLookup.Update(ref state);
            _bookingLookup.Update(ref state);
            _dockingBufferLookup.Update(ref state);

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var tick = tickTimeState.Tick;

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process destroyed nodes
            foreach (var (node, nodeEntity) in SystemAPI.Query<RefRO<WarpRelayNode>>()
                .WithAll<WarpRelayNodeTag>()
                .WithEntityAccess())
            {
                var nodeValue = node.ValueRO;

                if (nodeValue.Status != WarpRelayNodeStatus.Destroyed)
                {
                    continue;
                }

                // Mark bookings currently in transit on links to/from this node as failed
                foreach (var (booking, bookingEntity) in SystemAPI.Query<RefRW<WarpBooking>>().WithEntityAccess())
                {
                    var bookingValue = booking.ValueRO;

                    if (bookingValue.State == WarpBookingState.InTransit)
                    {
                        // Check if booking is on a link involving this node
                        if (bookingValue.OriginNodeId == nodeValue.NodeId ||
                            bookingValue.DestinationNodeId == nodeValue.NodeId ||
                            bookingValue.CurrentLinkId == nodeValue.NodeId) // Simplified check
                        {
                            // Mark as failed with unknown loss reason
                            booking.ValueRW.State = WarpBookingState.Failed;
                            // TODO: Add reason component: UnknownLossReason, EntityMissingInTransit
                        }
                    }

                    // Cancel bookings queued at destroyed node
                    if (bookingValue.OriginNodeId == nodeValue.NodeId &&
                        (bookingValue.State == WarpBookingState.QueuedAtOriginNode ||
                         bookingValue.State == WarpBookingState.Loading))
                    {
                        booking.ValueRW.State = WarpBookingState.Failed;
                    }
                }

                // Release any docked entities (spit them out at random nearby coordinates)
                if (_dockingBufferLookup.HasBuffer(nodeEntity))
                {
                    var dockingBuffer = _dockingBufferLookup[nodeEntity];
                    // TODO: Spawn entities at random nearby coordinates
                    // For now, just clear the buffer
                    dockingBuffer.Clear();
                }

                // Generate InfoPacket about destruction (if possible)
                // TODO: When comms system exists:
                // 1. Check if node got chance to send last message
                // 2. Generate InfoPacket with NodeDestroyed fact
                // 3. If not (instant kill, jamming), remote factions see:
                //    - No arrivals
                //    - No responses
                //    - Increasing age/out-of-contact
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

