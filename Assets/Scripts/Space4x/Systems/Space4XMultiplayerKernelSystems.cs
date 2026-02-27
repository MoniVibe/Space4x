using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class Space4XMultiplayerKernelSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XMultiplayerKernelBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XMultiplayerKernelRootTag>(out _))
            {
                state.Enabled = false;
                return;
            }

            var entityManager = state.EntityManager;
            var root = entityManager.CreateEntity(
                typeof(Space4XMultiplayerKernelRootTag),
                typeof(Space4XMultiplayerKernelMeta),
                typeof(Space4XMultiplayerKernelConfig),
                typeof(Space4XMultiplayerKernelState),
                typeof(Space4XMultiplayerKernelOverflow),
                typeof(Space4XMultiplayerKernelGuard));

            entityManager.SetComponentData(root, new Space4XMultiplayerKernelMeta
            {
                SchemaVersion = Space4XMultiplayerKernelMeta.CurrentSchemaVersion,
                KernelVersion = Space4XMultiplayerKernelMeta.CurrentKernelVersion
            });
            entityManager.SetComponentData(root, Space4XMultiplayerKernelConfig.Default);
            entityManager.SetComponentData(root, new Space4XMultiplayerKernelState
            {
                SessionSerial = 1u
            });
            entityManager.SetComponentData(root, default(Space4XMultiplayerKernelOverflow));
            entityManager.SetComponentData(root, default(Space4XMultiplayerKernelGuard));
            entityManager.AddBuffer<Space4XMultiplayerConnectionRequest>(root);
            entityManager.AddBuffer<Space4XMultiplayerPeer>(root);
            entityManager.AddBuffer<Space4XMultiplayerSessionEvent>(root);

            state.Enabled = false;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(Space4XMultiplayerKernelSystemGroup))]
    public partial struct Space4XMultiplayerKernelTickSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XMultiplayerKernelRootTag>();
            state.RequireForUpdate<Space4XMultiplayerKernelConfig>();
            state.RequireForUpdate<Space4XMultiplayerKernelState>();
            state.RequireForUpdate<Space4XMultiplayerKernelOverflow>();
            state.RequireForUpdate<Space4XMultiplayerKernelGuard>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<Space4XMultiplayerKernelRootTag>();
            var config = SystemAPI.GetComponentRO<Space4XMultiplayerKernelConfig>(root);
            if (config.ValueRO.Enabled == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var runtime = SystemAPI.GetComponentRW<Space4XMultiplayerKernelState>(root);
            var overflow = SystemAPI.GetComponentRW<Space4XMultiplayerKernelOverflow>(root);
            var guard = SystemAPI.GetComponentRW<Space4XMultiplayerKernelGuard>(root);
            var requests = SystemAPI.GetBuffer<Space4XMultiplayerConnectionRequest>(root);
            var peers = SystemAPI.GetBuffer<Space4XMultiplayerPeer>(root);
            var events = SystemAPI.GetBuffer<Space4XMultiplayerSessionEvent>(root);

            runtime.ValueRW.LastTick = timeState.Tick;
            overflow.ValueRW.DroppedThisTick = 0u;

            var maxRequestsPerTick = math.max(1u, config.ValueRO.MaxRequestsPerTick);
            var consumed = math.min(requests.Length, (int)maxRequestsPerTick);

            for (var i = 0; i < consumed; i++)
            {
                var request = requests[i];
                ProcessRequest(
                    ref runtime.ValueRW,
                    ref guard.ValueRW,
                    in config.ValueRO,
                    peers,
                    events,
                    in request,
                    timeState.Tick);
            }

            if (consumed < requests.Length)
            {
                var dropped = (uint)(requests.Length - consumed);
                overflow.ValueRW.DroppedThisTick = dropped;
                overflow.ValueRW.DroppedTotal += dropped;
                overflow.ValueRW.LastOverflowTick = timeState.Tick;
            }

            requests.Clear();
            runtime.ValueRW.ActivePeers = (uint)peers.Length;
        }

        private static void ProcessRequest(
            ref Space4XMultiplayerKernelState runtime,
            ref Space4XMultiplayerKernelGuard guard,
            in Space4XMultiplayerKernelConfig config,
            DynamicBuffer<Space4XMultiplayerPeer> peers,
            DynamicBuffer<Space4XMultiplayerSessionEvent> events,
            in Space4XMultiplayerConnectionRequest request,
            uint tick)
        {
            switch (request.Kind)
            {
                case Space4XMultiplayerConnectionRequestKind.Connect:
                    ProcessConnect(
                        ref runtime,
                        ref guard,
                        in config,
                        peers,
                        events,
                        in request,
                        tick);
                    break;

                case Space4XMultiplayerConnectionRequestKind.Disconnect:
                    ProcessDisconnect(
                        ref runtime,
                        ref guard,
                        in config,
                        peers,
                        events,
                        in request,
                        tick);
                    break;

                default:
                    Reject(
                        ref runtime,
                        ref guard,
                        in config,
                        events,
                        Space4XMultiplayerSessionEventKind.Connect,
                        Space4XMultiplayerRejectReason.InvalidRequest,
                        in request,
                        tick);
                    break;
            }
        }

        private static void ProcessConnect(
            ref Space4XMultiplayerKernelState runtime,
            ref Space4XMultiplayerKernelGuard guard,
            in Space4XMultiplayerKernelConfig config,
            DynamicBuffer<Space4XMultiplayerPeer> peers,
            DynamicBuffer<Space4XMultiplayerSessionEvent> events,
            in Space4XMultiplayerConnectionRequest request,
            uint tick)
        {
            if (request.PeerId == 0u)
            {
                Reject(
                    ref runtime,
                    ref guard,
                    in config,
                    events,
                    Space4XMultiplayerSessionEventKind.Connect,
                    Space4XMultiplayerRejectReason.InvalidRequest,
                    in request,
                    tick);
                return;
            }

            if (!AcceptsRemotePeers(config.LocalRole))
            {
                Reject(
                    ref runtime,
                    ref guard,
                    in config,
                    events,
                    Space4XMultiplayerSessionEventKind.Connect,
                    Space4XMultiplayerRejectReason.LocalRoleUnavailable,
                    in request,
                    tick);
                return;
            }

            if (FindPeerIndex(peers, request.PeerId) >= 0)
            {
                Reject(
                    ref runtime,
                    ref guard,
                    in config,
                    events,
                    Space4XMultiplayerSessionEventKind.Connect,
                    Space4XMultiplayerRejectReason.DuplicatePeer,
                    in request,
                    tick);
                return;
            }

            if (request.ProtocolVersion != config.ExpectedProtocolVersion)
            {
                Reject(
                    ref runtime,
                    ref guard,
                    in config,
                    events,
                    Space4XMultiplayerSessionEventKind.Connect,
                    Space4XMultiplayerRejectReason.ProtocolVersionMismatch,
                    in request,
                    tick);
                return;
            }

            if (request.ProtocolHash32 != config.ExpectedProtocolHash32)
            {
                Reject(
                    ref runtime,
                    ref guard,
                    in config,
                    events,
                    Space4XMultiplayerSessionEventKind.Connect,
                    Space4XMultiplayerRejectReason.ProtocolHashMismatch,
                    in request,
                    tick);
                return;
            }

            var maxPeers = math.max(1u, config.MaxPeers);
            if ((uint)peers.Length >= maxPeers)
            {
                Reject(
                    ref runtime,
                    ref guard,
                    in config,
                    events,
                    Space4XMultiplayerSessionEventKind.Connect,
                    Space4XMultiplayerRejectReason.CapacityExceeded,
                    in request,
                    tick);
                return;
            }

            peers.Add(new Space4XMultiplayerPeer
            {
                PeerId = request.PeerId,
                Role = (byte)ResolvePeerRole(request.RequestedRole),
                Connected = 1,
                Reserved0 = 0,
                ConnectedTick = tick,
                LastSeenTick = tick
            });

            runtime.AcceptedConnectionsTotal += 1u;
            AppendEvent(
                ref runtime,
                in config,
                events,
                tick,
                Space4XMultiplayerSessionEventKind.Connect,
                accepted: true,
                Space4XMultiplayerRejectReason.None,
                in request);
        }

        private static void ProcessDisconnect(
            ref Space4XMultiplayerKernelState runtime,
            ref Space4XMultiplayerKernelGuard guard,
            in Space4XMultiplayerKernelConfig config,
            DynamicBuffer<Space4XMultiplayerPeer> peers,
            DynamicBuffer<Space4XMultiplayerSessionEvent> events,
            in Space4XMultiplayerConnectionRequest request,
            uint tick)
        {
            if (request.PeerId == 0u)
            {
                Reject(
                    ref runtime,
                    ref guard,
                    in config,
                    events,
                    Space4XMultiplayerSessionEventKind.Disconnect,
                    Space4XMultiplayerRejectReason.InvalidRequest,
                    in request,
                    tick);
                return;
            }

            var index = FindPeerIndex(peers, request.PeerId);
            if (index < 0)
            {
                Reject(
                    ref runtime,
                    ref guard,
                    in config,
                    events,
                    Space4XMultiplayerSessionEventKind.Disconnect,
                    Space4XMultiplayerRejectReason.UnknownPeer,
                    in request,
                    tick);
                return;
            }

            peers.RemoveAtSwapBack(index);
            runtime.DisconnectsTotal += 1u;

            AppendEvent(
                ref runtime,
                in config,
                events,
                tick,
                Space4XMultiplayerSessionEventKind.Disconnect,
                accepted: true,
                Space4XMultiplayerRejectReason.None,
                in request);
        }

        private static void Reject(
            ref Space4XMultiplayerKernelState runtime,
            ref Space4XMultiplayerKernelGuard guard,
            in Space4XMultiplayerKernelConfig config,
            DynamicBuffer<Space4XMultiplayerSessionEvent> events,
            Space4XMultiplayerSessionEventKind kind,
            Space4XMultiplayerRejectReason reason,
            in Space4XMultiplayerConnectionRequest request,
            uint tick)
        {
            runtime.RejectedConnectionsTotal += 1u;
            guard.LastRejectTick = tick;

            switch (reason)
            {
                case Space4XMultiplayerRejectReason.ProtocolVersionMismatch:
                    guard.ProtocolVersionMismatchTotal += 1u;
                    break;
                case Space4XMultiplayerRejectReason.ProtocolHashMismatch:
                    guard.ProtocolHashMismatchTotal += 1u;
                    break;
                case Space4XMultiplayerRejectReason.CapacityExceeded:
                    guard.CapacityRejectTotal += 1u;
                    break;
                case Space4XMultiplayerRejectReason.DuplicatePeer:
                    guard.DuplicatePeerRejectTotal += 1u;
                    break;
                case Space4XMultiplayerRejectReason.UnknownPeer:
                    guard.UnknownPeerRejectTotal += 1u;
                    break;
                case Space4XMultiplayerRejectReason.LocalRoleUnavailable:
                    guard.LocalRoleRejectTotal += 1u;
                    break;
                default:
                    guard.InvalidRequestTotal += 1u;
                    break;
            }

            AppendEvent(
                ref runtime,
                in config,
                events,
                tick,
                kind,
                accepted: false,
                reason,
                in request);
        }

        private static bool AcceptsRemotePeers(Space4XMultiplayerRole role)
        {
            return role == Space4XMultiplayerRole.Host || role == Space4XMultiplayerRole.DedicatedServer;
        }

        private static Space4XMultiplayerRole ResolvePeerRole(byte requestedRole)
        {
            var role = (Space4XMultiplayerRole)requestedRole;
            return role == Space4XMultiplayerRole.Client ? Space4XMultiplayerRole.Client : Space4XMultiplayerRole.Client;
        }

        private static int FindPeerIndex(DynamicBuffer<Space4XMultiplayerPeer> peers, uint peerId)
        {
            for (var i = 0; i < peers.Length; i++)
            {
                if (peers[i].PeerId == peerId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void AppendEvent(
            ref Space4XMultiplayerKernelState runtime,
            in Space4XMultiplayerKernelConfig config,
            DynamicBuffer<Space4XMultiplayerSessionEvent> events,
            uint tick,
            Space4XMultiplayerSessionEventKind kind,
            bool accepted,
            Space4XMultiplayerRejectReason rejectReason,
            in Space4XMultiplayerConnectionRequest request)
        {
            runtime.LastEventSerial += 1u;
            events.Add(new Space4XMultiplayerSessionEvent
            {
                Serial = runtime.LastEventSerial,
                Tick = tick,
                Kind = kind,
                Accepted = accepted ? (byte)1 : (byte)0,
                RejectReason = rejectReason,
                Reserved0 = 0,
                PeerId = request.PeerId,
                ProtocolVersion = request.ProtocolVersion,
                ProtocolHash32 = request.ProtocolHash32,
                ContextFlags = request.ContextFlags
            });

            var maxRetained = math.max(16u, config.MaxRetainedEvents);
            if (events.Length > maxRetained)
            {
                var removeCount = events.Length - (int)maxRetained;
                events.RemoveRange(0, removeCount);
            }
        }
    }

    [UpdateInGroup(typeof(Space4XMultiplayerKernelSystemGroup))]
    [UpdateAfter(typeof(Space4XMultiplayerKernelTickSystem))]
    public partial struct Space4XMultiplayerKernelTelemetrySystem : ISystem
    {
        private static readonly FixedString64Bytes MetricRole = new FixedString64Bytes("space4x.mp.role");
        private static readonly FixedString64Bytes MetricProtocolVersion = new FixedString64Bytes("space4x.mp.protocol.version");
        private static readonly FixedString64Bytes MetricProtocolHash24 = new FixedString64Bytes("space4x.mp.protocol.hash24");
        private static readonly FixedString64Bytes MetricPeersActive = new FixedString64Bytes("space4x.mp.peers.active");
        private static readonly FixedString64Bytes MetricConnectAccepted = new FixedString64Bytes("space4x.mp.connect.accepted_total");
        private static readonly FixedString64Bytes MetricConnectRejected = new FixedString64Bytes("space4x.mp.connect.rejected_total");
        private static readonly FixedString64Bytes MetricDisconnects = new FixedString64Bytes("space4x.mp.disconnect.total");
        private static readonly FixedString64Bytes MetricDroppedRequests = new FixedString64Bytes("space4x.mp.intake.dropped_total");
        private static readonly FixedString64Bytes MetricEventSerial = new FixedString64Bytes("space4x.mp.event.serial");
        private static readonly FixedString64Bytes MetricVersionMismatch = new FixedString64Bytes("space4x.mp.guard.version_mismatch");
        private static readonly FixedString64Bytes MetricHashMismatch = new FixedString64Bytes("space4x.mp.guard.hash_mismatch");
        private static readonly FixedString64Bytes MetricCapacityReject = new FixedString64Bytes("space4x.mp.guard.capacity_reject");
        private static readonly FixedString64Bytes MetricDuplicatePeer = new FixedString64Bytes("space4x.mp.guard.duplicate_peer");
        private static readonly FixedString64Bytes MetricUnknownPeer = new FixedString64Bytes("space4x.mp.guard.unknown_peer");
        private static readonly FixedString64Bytes MetricLocalRoleReject = new FixedString64Bytes("space4x.mp.guard.local_role_reject");

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XMultiplayerKernelRootTag>();
            state.RequireForUpdate<Space4XMultiplayerKernelConfig>();
            state.RequireForUpdate<Space4XMultiplayerKernelState>();
            state.RequireForUpdate<Space4XMultiplayerKernelOverflow>();
            state.RequireForUpdate<Space4XMultiplayerKernelGuard>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            if (!TryGetTelemetryMetricBuffer(ref state, out var metricBuffer))
            {
                return;
            }

            var root = SystemAPI.GetSingletonEntity<Space4XMultiplayerKernelRootTag>();
            var kernelConfig = SystemAPI.GetComponent<Space4XMultiplayerKernelConfig>(root);
            var runtime = SystemAPI.GetComponent<Space4XMultiplayerKernelState>(root);
            var overflow = SystemAPI.GetComponent<Space4XMultiplayerKernelOverflow>(root);
            var guard = SystemAPI.GetComponent<Space4XMultiplayerKernelGuard>(root);

            metricBuffer.AddMetric(MetricRole, (uint)kernelConfig.LocalRole, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricProtocolVersion, kernelConfig.ExpectedProtocolVersion, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricProtocolHash24, kernelConfig.ExpectedProtocolHash32 & 0x00FFFFFFu, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricPeersActive, runtime.ActivePeers, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricConnectAccepted, runtime.AcceptedConnectionsTotal, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricConnectRejected, runtime.RejectedConnectionsTotal, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricDisconnects, runtime.DisconnectsTotal, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricDroppedRequests, overflow.DroppedTotal, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricEventSerial, runtime.LastEventSerial, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricVersionMismatch, guard.ProtocolVersionMismatchTotal, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricHashMismatch, guard.ProtocolHashMismatchTotal, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricCapacityReject, guard.CapacityRejectTotal, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricDuplicatePeer, guard.DuplicatePeerRejectTotal, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricUnknownPeer, guard.UnknownPeerRejectTotal, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric(MetricLocalRoleReject, guard.LocalRoleRejectTotal, TelemetryMetricUnit.Count);
        }

        private static bool TryGetTelemetryMetricBuffer(ref SystemState state, out DynamicBuffer<TelemetryMetric> buffer)
        {
            buffer = default;
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var telemetryEntity = query.GetSingletonEntity();
            if (telemetryEntity == Entity.Null || !state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            return true;
        }
    }
}
