using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Transport.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Systems
{
    /// <summary>
    /// Manages booking queues at warp relay nodes.
    /// Collects bookings, checks schedule conditions, selects bookings for departure.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WarpRelayQueueSystem : ISystem
    {
        private ComponentLookup<WarpRelayNode> _nodeLookup;
        private ComponentLookup<WarpRelayDriveBank> _driveBankLookup;
        private ComponentLookup<WarpBooking> _bookingLookup;
        private ComponentLookup<HyperwayServiceDef> _serviceDefLookup;
        private ComponentLookup<WarpRelayServiceState> _serviceStateLookup;
        private BufferLookup<WarpRelayDocking> _dockingBufferLookup;
        private BufferLookup<WarpRelayQueueElement> _queueBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _nodeLookup = state.GetComponentLookup<WarpRelayNode>(false);
            _driveBankLookup = state.GetComponentLookup<WarpRelayDriveBank>(false);
            _bookingLookup = state.GetComponentLookup<WarpBooking>(false);
            _serviceDefLookup = state.GetComponentLookup<HyperwayServiceDef>(false);
            _serviceStateLookup = state.GetComponentLookup<WarpRelayServiceState>(false);
            _dockingBufferLookup = state.GetBufferLookup<WarpRelayDocking>(false);
            _queueBufferLookup = state.GetBufferLookup<WarpRelayQueueElement>(false);
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
            _driveBankLookup.Update(ref state);
            _bookingLookup.Update(ref state);
            _serviceDefLookup.Update(ref state);
            _serviceStateLookup.Update(ref state);
            _dockingBufferLookup.Update(ref state);
            _queueBufferLookup.Update(ref state);

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var tick = tickTimeState.Tick;

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process each warp relay node
            foreach (var (node, nodeEntity) in SystemAPI.Query<RefRO<WarpRelayNode>>()
                .WithAll<WarpRelayNodeTag>()
                .WithEntityAccess())
            {
                var nodeValue = node.ValueRO;

                // Only process online nodes
                if (nodeValue.Status != WarpRelayNodeStatus.Online)
                {
                    continue;
                }

                // Get drive bank
                if (!_driveBankLookup.TryGetComponent(nodeEntity, out var driveBank))
                {
                    continue;
                }

                // Collect bookings queued at this node
                var queuedBookings = new NativeList<Entity>(Allocator.Temp);
                foreach (var (booking, bookingEntity) in SystemAPI.Query<RefRO<WarpBooking>>().WithEntityAccess())
                {
                    if (booking.ValueRO.OriginNodeId == nodeValue.NodeId &&
                        booking.ValueRO.State == WarpBookingState.QueuedAtOriginNode)
                    {
                        queuedBookings.Add(bookingEntity);
                    }
                }

                if (queuedBookings.Length == 0)
                {
                    queuedBookings.Dispose();
                    continue;
                }

                // Get service definitions for links from this node
                // For now, simplified - check if any service is ready to depart
                bool shouldDepart = false;

                // Check interval-based schedules
                foreach (var (serviceDef, serviceEntity) in SystemAPI.Query<RefRO<HyperwayServiceDef>>().WithEntityAccess())
                {
                    if (serviceDef.ValueRO.Mode == HyperwayScheduleMode.Interval)
                    {
                        if (_serviceStateLookup.TryGetComponent(serviceEntity, out var serviceState))
                        {
                            uint ticksSinceLastDeparture = tick - serviceState.LastDepartureTick;
                            if (ticksSinceLastDeparture >= serviceDef.ValueRO.IntervalTicks)
                            {
                                shouldDepart = true;
                                break;
                            }
                        }
                        else
                        {
                            // No previous departure, depart immediately if interval elapsed
                            shouldDepart = true;
                        }
                    }
                }

                if (!shouldDepart)
                {
                    queuedBookings.Dispose();
                    continue;
                }

                // Select bookings to fill capacity
                float totalMass = 0f;
                float totalVolume = 0f;
                var selectedBookings = new NativeList<Entity>(Allocator.Temp);

                for (int i = 0; i < queuedBookings.Length; i++)
                {
                    var bookingEntity = queuedBookings[i];
                    if (!_bookingLookup.TryGetComponent(bookingEntity, out var booking))
                    {
                        continue;
                    }

                    float newMass = totalMass + booking.TotalMass;
                    float newVolume = totalVolume + booking.TotalVolume;

                    if (newMass <= driveBank.MaxPayloadMass && newVolume <= driveBank.MaxPayloadVolume)
                    {
                        selectedBookings.Add(bookingEntity);
                        totalMass = newMass;
                        totalVolume = newVolume;
                    }
                }

                // Add selected bookings to docking
                if (!_dockingBufferLookup.HasBuffer(nodeEntity))
                {
                    ecb.AddBuffer<WarpRelayDocking>(nodeEntity);
                }

                var dockingBuffer = _dockingBufferLookup[nodeEntity];
                for (int i = 0; i < selectedBookings.Length; i++)
                {
                    var bookingEntity = selectedBookings[i];
                    var booking = _bookingLookup.GetRefRW(bookingEntity);

                    // Add to docking buffer
                    dockingBuffer.Add(new WarpRelayDocking
                    {
                        DockedEntity = booking.ValueRO.Traveller,
                        Mass = booking.ValueRO.TotalMass,
                        Volume = booking.ValueRO.TotalVolume
                    });

                    // Update booking state
                    booking.ValueRW.State = WarpBookingState.InTransit;
                    booking.ValueRW.ExpectedDepartureTick = tick;
                    // ExpectedArrivalTick would be set based on link travel time
                }

                // Update service state
                foreach (var (serviceDef, serviceEntity) in SystemAPI.Query<RefRO<HyperwayServiceDef>>().WithEntityAccess())
                {
                    if (!_serviceStateLookup.HasComponent(serviceEntity))
                    {
                        ecb.AddComponent(serviceEntity, new WarpRelayServiceState
                        {
                            LinkId = serviceDef.ValueRO.LinkId
                        });
                    }

                    var serviceStateRef = _serviceStateLookup.GetRefRW(serviceEntity);
                    serviceStateRef.ValueRW.LastDepartureTick = tick;
                    serviceStateRef.ValueRW.QueuedValue = 0f; // Reset after departure
                }

                queuedBookings.Dispose();
                selectedBookings.Dispose();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

