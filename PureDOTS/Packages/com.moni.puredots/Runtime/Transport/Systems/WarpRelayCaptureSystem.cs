using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Transport.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Systems
{
    /// <summary>
    /// Handles warp relay node capture.
    /// Manages ownership changes, access contract rewrites, and booking cancellation/traps.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WarpRelayCaptureSystem : ISystem
    {
        private ComponentLookup<WarpRelayNode> _nodeLookup;
        private ComponentLookup<WarpBooking> _bookingLookup;
        private BufferLookup<HyperwayAccessContract> _accessContractBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _nodeLookup = state.GetComponentLookup<WarpRelayNode>(false);
            _bookingLookup = state.GetComponentLookup<WarpBooking>(false);
            _accessContractBufferLookup = state.GetBufferLookup<HyperwayAccessContract>(false);
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
            _accessContractBufferLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process nodes that have been captured
            foreach (var (node, nodeEntity) in SystemAPI.Query<RefRW<WarpRelayNode>>()
                .WithAll<WarpRelayNodeTag>()
                .WithEntityAccess())
            {
                var nodeValue = node.ValueRO;

                // Check if node was just captured
                if (nodeValue.Status == WarpRelayNodeStatus.Captured)
                {
                    // Rewrite access contracts
                    // Clear existing contracts and create new ones based on new owner
                    if (_accessContractBufferLookup.HasBuffer(nodeEntity))
                    {
                        var contracts = _accessContractBufferLookup[nodeEntity];
                        contracts.Clear();

                        // Add contract for new owner with full access
                        contracts.Add(new HyperwayAccessContract
                        {
                            FactionId = nodeValue.OwnerFactionId,
                            AccessLevel = HyperwayAccessLevel.FullAccess,
                            DiscountFactor = 1.0f // Full discount for owner
                        });
                    }
                    else
                    {
                        var contracts = ecb.AddBuffer<HyperwayAccessContract>(nodeEntity);
                        contracts.Add(new HyperwayAccessContract
                        {
                            FactionId = nodeValue.OwnerFactionId,
                            AccessLevel = HyperwayAccessLevel.FullAccess,
                            DiscountFactor = 1.0f
                        });
                    }

                    // Cancel or trap enemy bookings
                    foreach (var (booking, bookingEntity) in SystemAPI.Query<RefRW<WarpBooking>>().WithEntityAccess())
                    {
                        var bookingValue = booking.ValueRO;

                        // Check if booking is at this node or en route to/from it
                        if (bookingValue.OriginNodeId == nodeValue.NodeId ||
                            bookingValue.DestinationNodeId == nodeValue.NodeId)
                        {
                            // TODO: Check if booking's faction is enemy of new owner
                            // For now, cancel all bookings at captured node
                            if (bookingValue.State == WarpBookingState.QueuedAtOriginNode ||
                                bookingValue.State == WarpBookingState.Loading)
                            {
                                booking.ValueRW.State = WarpBookingState.Failed;
                            }
                            // InTransit bookings could be turned into traps
                            // (arrive at captured node, get ambushed)
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

