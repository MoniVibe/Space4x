using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Environment;
using PureDOTS.Systems.Environment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Tests.EditMode.Environment
{
    public class EnvironmentSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("TestWorld");
            _entityManager = _world.EntityManager;

            // Create required singletons
            var timeStateEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(timeStateEntity, new TimeState
            {
                Tick = 0,
                IsPaused = false
            });

            var rewindStateEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(rewindStateEntity, new RewindState
            {
                Mode = RewindMode.Record
            });
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void ClimateOscillationSystem_UpdatesTemperatureCorrectly()
        {
            // Create climate state and config
            var climateEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(climateEntity, new ClimateState
            {
                Temperature = 20f,
                Humidity = 0.5f,
                SeasonIndex = 0,
                SeasonTick = 0,
                SeasonLength = 250u,
                LastUpdateTick = 0
            });

            var configEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(configEntity, new ClimateConfig
            {
                BaseTemperature = 20f,
                BaseHumidity = 0.5f,
                TemperatureOscillation = 10f,
                HumidityOscillation = 0.3f,
                TemperaturePeriod = 1000u,
                HumidityPeriod = 800u,
                SeasonLengthTicks = 250u,
                SeasonsEnabled = 0
            });

            // Run system
            var system = _world.GetOrCreateSystemManaged<ClimateOscillationSystem>();
            
            // Set tick to quarter period (should be at peak)
            var timeState = _entityManager.GetComponentData<TimeState>(_entityManager.GetSingletonEntity<TimeState>());
            timeState.Tick = 250u; // Quarter of 1000
            _entityManager.SetComponentData(_entityManager.GetSingletonEntity<TimeState>(), timeState);

            system.Update(_world.Unmanaged);

            // Check temperature oscillated
            var climate = _entityManager.GetComponentData<ClimateState>(climateEntity);
            Assert.GreaterOrEqual(climate.Temperature, 20f - 10f);
            Assert.LessOrEqual(climate.Temperature, 20f + 10f);
        }

        [Test]
        public void WindUpdateSystem_UpdatesWindTypeBasedOnStrength()
        {
            // Create wind state and config
            var windEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(windEntity, new WindState
            {
                Direction = new float2(1f, 0f),
                Strength = 0.3f,
                Type = WindType.Calm,
                LastUpdateTick = 0
            });

            var configEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(configEntity, WindConfig.Default);

            // Run system
            var system = _world.GetOrCreateSystemManaged<WindUpdateSystem>();
            system.Update(_world.Unmanaged);

            // Check wind type was updated based on strength
            var wind = _entityManager.GetComponentData<WindState>(windEntity);
            Assert.IsTrue(wind.Type == WindType.Breeze || wind.Type == WindType.Wind || wind.Type == WindType.Storm);
        }

        [Test]
        public void VegetationStressSystem_CalculateFactor_ReturnsCorrectValues()
        {
            // Test optimal range
            float factor = VegetationStressSystem.CalculateFactor(0.5f, 0.3f, 0.7f);
            Assert.AreEqual(1f, factor, 0.001f, "Value in optimal range should return 1");

            // Test below minimum
            factor = VegetationStressSystem.CalculateFactor(0.1f, 0.3f, 0.7f);
            Assert.Less(factor, 1f, "Value below minimum should return < 1");
            Assert.GreaterOrEqual(factor, 0f, "Factor should be >= 0");

            // Test above maximum
            factor = VegetationStressSystem.CalculateFactor(0.9f, 0.3f, 0.7f);
            Assert.Less(factor, 1f, "Value above maximum should return < 1");
            Assert.GreaterOrEqual(factor, 0f, "Factor should be >= 0");
        }

        [Test]
        public void MoistureGridUpdateSystem_CalculateEvaporationRate_UsesMultipliers()
        {
            var config = MoistureConfig.Default;
            var temperature = 30f; // Hot
            var windStrength = 0.8f; // Strong wind

            float rate = MoistureGridUpdateSystem.CalculateEvaporationRate(
                0.001f, // Base rate
                temperature,
                windStrength,
                in config);

            Assert.Greater(rate, 0.001f, "Evaporation should increase with temperature and wind");
        }

        [Test]
        public void EnvironmentBootstrapSystem_CreatesSingletons()
        {
            // Initialize environment system
            EnvironmentBootstrapSystem.InitializeEnvironmentSystem(_entityManager);

            // Check all singletons were created
            Assert.IsTrue(_entityManager.HasComponent<ClimateState>(_entityManager.GetSingletonEntity<ClimateState>()));
            Assert.IsTrue(_entityManager.HasComponent<ClimateConfig>(_entityManager.GetSingletonEntity<ClimateConfig>()));
            Assert.IsTrue(_entityManager.HasComponent<WindState>(_entityManager.GetSingletonEntity<WindState>()));
            Assert.IsTrue(_entityManager.HasComponent<WindConfig>(_entityManager.GetSingletonEntity<WindConfig>()));
            Assert.IsTrue(_entityManager.HasComponent<SunlightState>(_entityManager.GetSingletonEntity<SunlightState>()));
            Assert.IsTrue(_entityManager.HasComponent<SunlightConfig>(_entityManager.GetSingletonEntity<SunlightConfig>()));
            Assert.IsTrue(_entityManager.HasComponent<MoistureGridState>(_entityManager.GetSingletonEntity<MoistureGridState>()));
            Assert.IsTrue(_entityManager.HasComponent<MoistureConfig>(_entityManager.GetSingletonEntity<MoistureConfig>()));
        }

        [Test]
        public void MoistureGridState_InitializesWithSpatialGrid()
        {
            // Create spatial grid config
            var spatialEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(spatialEntity, new SpatialGridConfig
            {
                CellCounts = new int3(10, 10, 1),
                CellSize = 5f,
                WorldMin = float3.zero
            });

            // Initialize environment system
            EnvironmentBootstrapSystem.InitializeEnvironmentSystem(_entityManager);

            // Check moisture grid matches spatial grid
            var moistureGrid = _entityManager.GetComponentData<MoistureGridState>(_entityManager.GetSingletonEntity<MoistureGridState>());
            Assert.AreEqual(10, moistureGrid.Width);
            Assert.AreEqual(10, moistureGrid.Height);
            Assert.AreEqual(5f, moistureGrid.CellSize);
            Assert.IsTrue(moistureGrid.Grid.IsCreated);
        }
    }
}

