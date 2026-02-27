#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Power;
using Space4X.Physics;
using Space4X.Runtime;
using Space4X.Systems.Heat;
using Space4X.Tests.TestHarness;
using Space4x.Fleetcrawl;
using Unity.Entities;
using Unity.Mathematics;
using PowerHeatState = PureDOTS.Runtime.Power.HeatState;

namespace Space4X.Tests
{
    public class Space4XHeatKernelSystemsTests
    {
        private ISystemTestHarness _harness;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _harness = new ISystemTestHarness();
            _entityManager = _harness.World.EntityManager;
            EnsureTimeStateSingleton();
            _harness.Add<Space4XHeatKernelBootstrapSystem>();
            _harness.Add<Space4XHeatKernelUpdateSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
        }

        [Test]
        public void Bootstrap_AddsKernelComponents_ForSensorSignatureEntities()
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, SensorSignature.Default);

            _harness.Step();

            Assert.IsTrue(_entityManager.HasComponent<Space4XHeatKernelState>(entity));
            Assert.IsTrue(_entityManager.HasComponent<Space4XHeatKernelOutput>(entity));
            Assert.IsTrue(_entityManager.HasBuffer<Space4XHeatKernelActionEvent>(entity));

            var configQuery = _entityManager.CreateEntityQuery(typeof(Space4XHeatKernelConfig));
            Assert.AreEqual(1, configQuery.CalculateEntityCount(), "Heat kernel config singleton should be created once.");
        }

        [Test]
        public void Update_ProducesGenericThermal_FromPowerAndMotion()
        {
            var ship = _entityManager.CreateEntity();
            _entityManager.AddComponentData(ship, SensorSignature.Default);
            _entityManager.AddComponentData(ship, new SpaceVelocity
            {
                Linear = new float3(24f, 0f, 0f),
                Angular = float3.zero
            });
            _entityManager.AddComponentData(ship, new PowerLedger
            {
                GenerationMW = 120f,
                DistributionOutputMW = 120f,
                TotalAllocatedMW = 100f,
                BatteryDischargeMW = 0f
            });

            var consumers = _entityManager.AddBuffer<ShipPowerConsumer>(ship);
            var mobilityConsumer = _entityManager.CreateEntity();
            _entityManager.AddComponentData(mobilityConsumer, new PowerConsumer
            {
                BaselineDraw = 100f,
                RequestedDraw = 100f,
                MinOperatingFraction = 0.1f,
                Priority = 10,
                AllocatedDraw = 100f,
                Online = 1,
                Starved = 0
            });
            _entityManager.AddComponentData(mobilityConsumer, new PowerHeatState
            {
                CurrentHeat = 60f,
                MaxHeatCapacity = 100f,
                PassiveDissipation = 0f
            });
            consumers.Add(new ShipPowerConsumer
            {
                Type = ShipPowerConsumerType.Mobility,
                Consumer = mobilityConsumer
            });

            _harness.Step();

            var output = _entityManager.GetComponentData<Space4XHeatKernelOutput>(ship);
            Assert.Greater(output.Thermal01, 0.1f);
            Assert.Greater(output.PowerLoad01, 0.75f);
            Assert.Greater(output.ConsumerHeat01, 0.5f);
            Assert.AreEqual(Space4XHeatKernelSourceKind.Generic, output.SourceKind);
        }

        [Test]
        public void Update_UsesFleetcrawlThermal_WhenAvailable()
        {
            var ship = _entityManager.CreateEntity();
            _entityManager.AddComponentData(ship, SensorSignature.Default);
            _entityManager.AddComponentData(ship, new FleetcrawlHeatOutputState
            {
                Heat01 = 0.82f,
                HeatsinkStoredHeat = 20f,
                HeatsinkCapacity = 40f,
                IsOverheated = 0
            });

            _harness.Step();

            var configQuery = _entityManager.CreateEntityQuery(typeof(Space4XHeatKernelConfig));
            var configEntity = configQuery.GetSingletonEntity();
            var config = _entityManager.GetComponentData<Space4XHeatKernelConfig>(configEntity);
            config.ResponseLerp = 1f;
            config.GenericBlendWhenFleetcrawl = 0f;
            _entityManager.SetComponentData(configEntity, config);

            _harness.Step();

            var output = _entityManager.GetComponentData<Space4XHeatKernelOutput>(ship);
            var expected = FleetcrawlHeatResolver.ResolveHeatSignature01(_entityManager.GetComponentData<FleetcrawlHeatOutputState>(ship));
            Assert.AreEqual(Space4XHeatKernelSourceKind.Fleetcrawl, output.SourceKind);
            Assert.AreEqual(expected, output.Thermal01, 1e-4f);
        }

        private void EnsureTimeStateSingleton()
        {
            var timeEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(timeEntity, new TimeState
            {
                Tick = 1u,
                DeltaTime = 1f / 60f,
                DeltaSeconds = 1f / 60f,
                ElapsedTime = 0f,
                WorldSeconds = 0f,
                IsPaused = false,
                FixedDeltaTime = 1f / 60f,
                CurrentSpeedMultiplier = 1f
            });
        }
    }
}
#endif
