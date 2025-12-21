#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Space4X.Tests.TestHarness;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    /// <summary>
    /// Verifies carriers are auto-bootstrapped into fleet/intercept pipelines.
    /// </summary>
    public class Space4XCarrierFleetBootstrapSystemTests
    {
        private ISystemTestHarness _harness;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _harness = new ISystemTestHarness();
            _entityManager = _harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            _harness.Add<Space4XCarrierFleetBootstrapSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
        }

        [Test]
        public void CarrierGetsFleetAndBroadcastComponents()
        {
            var carrier = _entityManager.CreateEntity(typeof(Carrier), typeof(LocalTransform));
            _entityManager.SetComponentData(carrier, new Carrier
            {
                CarrierId = new Unity.Collections.FixedString64Bytes("CARRIER-BOOT"),
                AffiliationEntity = Entity.Null,
                Speed = 3f,
                PatrolCenter = float3.zero,
                PatrolRadius = 50f
            });
            _entityManager.SetComponentData(carrier, LocalTransform.FromPositionRotationScale(new float3(5f, 0f, 0f), quaternion.identity, 1f));

            _harness.Step();

            Assert.IsTrue(_entityManager.HasComponent<Space4XFleet>(carrier), "Carrier should receive Space4XFleet component");
            Assert.IsTrue(_entityManager.HasComponent<FleetMovementBroadcast>(carrier), "Carrier should receive FleetMovementBroadcast component");

            var fleet = _entityManager.GetComponentData<Space4XFleet>(carrier);
            Assert.IsFalse(fleet.FleetId.IsEmpty, "FleetId should be populated");

            var broadcast = _entityManager.GetComponentData<FleetMovementBroadcast>(carrier);
            Assert.AreEqual(new float3(5f, 0f, 0f), broadcast.Position, "Broadcast position should mirror carrier transform");
        }
    }
}
#endif
