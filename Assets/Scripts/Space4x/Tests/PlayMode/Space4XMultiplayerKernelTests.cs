#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using Space4X.Runtime;
using Space4X.Systems;
using Unity.Core;
using Unity.Entities;

namespace Space4X.Tests.PlayMode
{
    public class Space4XMultiplayerKernelTests
    {
        [Test]
        public void MultiplayerKernel_AcceptsMatchingProtocol_RejectsMismatches()
        {
            using var world = new World("Space4XMultiplayerKernelTests-Protocol");
            var entityManager = world.EntityManager;

            CoreSingletonBootstrapSystem.EnsureSingletons(entityManager);
            var timeEntity = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            SetTimeState(entityManager, timeEntity, tick: 0u);

            var bootstrap = world.GetOrCreateSystem<Space4XMultiplayerKernelBootstrapSystem>();
            var tickSystem = world.GetOrCreateSystem<Space4XMultiplayerKernelTickSystem>();
            bootstrap.Update(world.Unmanaged);

            var root = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XMultiplayerKernelRootTag>()).GetSingletonEntity();

            var config = entityManager.GetComponentData<Space4XMultiplayerKernelConfig>(root);
            config.LocalRole = Space4XMultiplayerRole.Host;
            config.ExpectedProtocolVersion = 3u;
            config.ExpectedProtocolHash32 = 0xC0FFEE12u;
            config.MaxPeers = 4u;
            entityManager.SetComponentData(root, config);

            var inbox = entityManager.GetBuffer<Space4XMultiplayerConnectionRequest>(root);
            inbox.Add(CreateConnectRequest(101u, version: 3u, hash32: 0xC0FFEE12u));
            inbox.Add(CreateConnectRequest(102u, version: 2u, hash32: 0xC0FFEE12u));
            inbox.Add(CreateConnectRequest(103u, version: 3u, hash32: 0xABCD1234u));

            AdvanceTick(world, entityManager, timeEntity, tickSystem, tick: 1u);

            var peers = entityManager.GetBuffer<Space4XMultiplayerPeer>(root);
            var runtime = entityManager.GetComponentData<Space4XMultiplayerKernelState>(root);
            var guard = entityManager.GetComponentData<Space4XMultiplayerKernelGuard>(root);
            var events = entityManager.GetBuffer<Space4XMultiplayerSessionEvent>(root);

            Assert.AreEqual(1, peers.Length, "Only one peer should be accepted.");
            Assert.AreEqual(101u, peers[0].PeerId);
            Assert.AreEqual(1u, runtime.AcceptedConnectionsTotal);
            Assert.AreEqual(2u, runtime.RejectedConnectionsTotal);
            Assert.AreEqual(1u, guard.ProtocolVersionMismatchTotal);
            Assert.AreEqual(1u, guard.ProtocolHashMismatchTotal);
            Assert.AreEqual((uint)peers.Length, runtime.ActivePeers);

            Assert.IsTrue(HasEvent(events, Space4XMultiplayerSessionEventKind.Connect, accepted: true, Space4XMultiplayerRejectReason.None, 101u));
            Assert.IsTrue(HasEvent(events, Space4XMultiplayerSessionEventKind.Connect, accepted: false, Space4XMultiplayerRejectReason.ProtocolVersionMismatch, 102u));
            Assert.IsTrue(HasEvent(events, Space4XMultiplayerSessionEventKind.Connect, accepted: false, Space4XMultiplayerRejectReason.ProtocolHashMismatch, 103u));
        }

        [Test]
        public void MultiplayerKernel_EnforcesCapacity_AndProcessesDisconnect()
        {
            using var world = new World("Space4XMultiplayerKernelTests-Capacity");
            var entityManager = world.EntityManager;

            CoreSingletonBootstrapSystem.EnsureSingletons(entityManager);
            var timeEntity = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            SetTimeState(entityManager, timeEntity, tick: 0u);

            var bootstrap = world.GetOrCreateSystem<Space4XMultiplayerKernelBootstrapSystem>();
            var tickSystem = world.GetOrCreateSystem<Space4XMultiplayerKernelTickSystem>();
            bootstrap.Update(world.Unmanaged);

            var root = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XMultiplayerKernelRootTag>()).GetSingletonEntity();

            var config = entityManager.GetComponentData<Space4XMultiplayerKernelConfig>(root);
            config.LocalRole = Space4XMultiplayerRole.DedicatedServer;
            config.ExpectedProtocolVersion = 1u;
            config.ExpectedProtocolHash32 = 0x12345678u;
            config.MaxPeers = 1u;
            entityManager.SetComponentData(root, config);

            var inbox = entityManager.GetBuffer<Space4XMultiplayerConnectionRequest>(root);
            inbox.Add(CreateConnectRequest(201u, version: 1u, hash32: 0x12345678u));
            inbox.Add(CreateConnectRequest(202u, version: 1u, hash32: 0x12345678u));

            AdvanceTick(world, entityManager, timeEntity, tickSystem, tick: 1u);

            var peersAfterConnect = entityManager.GetBuffer<Space4XMultiplayerPeer>(root);
            var runtimeAfterConnect = entityManager.GetComponentData<Space4XMultiplayerKernelState>(root);
            var guardAfterConnect = entityManager.GetComponentData<Space4XMultiplayerKernelGuard>(root);

            Assert.AreEqual(1, peersAfterConnect.Length, "Capacity=1 should keep exactly one peer.");
            Assert.AreEqual(201u, peersAfterConnect[0].PeerId);
            Assert.AreEqual(1u, runtimeAfterConnect.AcceptedConnectionsTotal);
            Assert.AreEqual(1u, runtimeAfterConnect.RejectedConnectionsTotal);
            Assert.AreEqual(1u, guardAfterConnect.CapacityRejectTotal);

            var disconnectInbox = entityManager.GetBuffer<Space4XMultiplayerConnectionRequest>(root);
            disconnectInbox.Add(CreateDisconnectRequest(201u));
            AdvanceTick(world, entityManager, timeEntity, tickSystem, tick: 2u);

            var peersAfterDisconnect = entityManager.GetBuffer<Space4XMultiplayerPeer>(root);
            var runtimeAfterDisconnect = entityManager.GetComponentData<Space4XMultiplayerKernelState>(root);
            var events = entityManager.GetBuffer<Space4XMultiplayerSessionEvent>(root);

            Assert.AreEqual(0, peersAfterDisconnect.Length);
            Assert.AreEqual(1u, runtimeAfterDisconnect.DisconnectsTotal);
            Assert.IsTrue(HasEvent(events, Space4XMultiplayerSessionEventKind.Disconnect, accepted: true, Space4XMultiplayerRejectReason.None, 201u));
        }

        private static Space4XMultiplayerConnectionRequest CreateConnectRequest(uint peerId, uint version, uint hash32)
        {
            return new Space4XMultiplayerConnectionRequest
            {
                Kind = Space4XMultiplayerConnectionRequestKind.Connect,
                RequestedRole = (byte)Space4XMultiplayerRole.Client,
                Reserved0 = 0,
                PeerId = peerId,
                ProtocolVersion = version,
                ProtocolHash32 = hash32,
                ContextFlags = 0u
            };
        }

        private static Space4XMultiplayerConnectionRequest CreateDisconnectRequest(uint peerId)
        {
            return new Space4XMultiplayerConnectionRequest
            {
                Kind = Space4XMultiplayerConnectionRequestKind.Disconnect,
                RequestedRole = 0,
                Reserved0 = 0,
                PeerId = peerId,
                ProtocolVersion = 0u,
                ProtocolHash32 = 0u,
                ContextFlags = 0u
            };
        }

        private static bool HasEvent(
            DynamicBuffer<Space4XMultiplayerSessionEvent> events,
            Space4XMultiplayerSessionEventKind kind,
            bool accepted,
            Space4XMultiplayerRejectReason rejectReason,
            uint peerId)
        {
            var acceptedByte = accepted ? (byte)1 : (byte)0;
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind == kind &&
                    evt.Accepted == acceptedByte &&
                    evt.RejectReason == rejectReason &&
                    evt.PeerId == peerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetTimeState(EntityManager entityManager, Entity timeEntity, uint tick)
        {
            var time = entityManager.GetComponentData<TimeState>(timeEntity);
            time.Tick = tick;
            time.FixedDeltaTime = 1f / 60f;
            time.DeltaTime = time.FixedDeltaTime;
            time.DeltaSeconds = time.FixedDeltaTime;
            time.ElapsedTime = tick * time.FixedDeltaTime;
            time.WorldSeconds = time.ElapsedTime;
            time.CurrentSpeedMultiplier = 1f;
            time.IsPaused = false;
            entityManager.SetComponentData(timeEntity, time);
        }

        private static void AdvanceTick(
            World world,
            EntityManager entityManager,
            Entity timeEntity,
            SystemHandle tickSystem,
            uint tick)
        {
            SetTimeState(entityManager, timeEntity, tick);
            var time = entityManager.GetComponentData<TimeState>(timeEntity);
            world.EntityManager.WorldUnmanaged.Time = new TimeData(time.ElapsedTime, time.FixedDeltaTime);
            tickSystem.Update(world.Unmanaged);
        }
    }
}
#endif
