using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    /// <summary>
    /// Integration tests for colony supply/demand calculations.
    /// </summary>
    public class Space4XColonySupplyTests
    {
        private World _world;
        private EntityManager _entityManager;

        private Entity _timeEntity;
        private SystemHandle _bridgeHandle;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ColonySupplyTests");
            _entityManager = _world.EntityManager;

            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            EnsureRegistryDirectory();

            _bridgeHandle = _world.GetOrCreateSystem<Space4XRegistryBridgeSystem>();

            _timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();

            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 0;
            time.FixedDeltaTime = 1f;
            time.IsPaused = false;
            _entityManager.SetComponentData(_timeEntity, time);
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated)
            {
                _world.Dispose();
            }
        }

        [Test]
        public void ColonySupplyDemand_CalculatedCorrectly()
        {
            // Create colony with population
            float population = 100000f;
            var colony = CreateColony("COLONY-1", population, 500f, Space4XColonyStatus.Growing, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify demand calculation
            float expectedDemand = Space4XColonySupply.ComputeDemand(population);
            Assert.AreEqual(population * Space4XColonySupply.DemandPerPopulation, expectedDemand, 0.01f,
                "Demand should be calculated correctly");

            // Verify supply ratio
            float supplyRatio = Space4XColonySupply.ComputeSupplyRatio(500f, expectedDemand);
            Assert.Greater(supplyRatio, 0f, "Supply ratio should be positive");
        }

        [Test]
        public void ColonySupplyRatio_ReflectedInRegistryFlags()
        {
            // Create colony with low stored resources (should trigger bottleneck)
            float population = 100000f;
            float demand = Space4XColonySupply.ComputeDemand(population);
            float lowStoredResources = demand * 0.5f; // 50% of demand (below bottleneck threshold)

            var colony = CreateColony("COLONY-1", population, lowStoredResources, Space4XColonyStatus.Growing, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify flags
            var colonyRegistryEntity = FindColonyRegistryEntity();
            var registryBuffer = _entityManager.GetBuffer<Space4XColonyRegistryEntry>(colonyRegistryEntity);

            bool found = false;
            for (int i = 0; i < registryBuffer.Length; i++)
            {
                if (registryBuffer[i].ColonyId.ToString() == "COLONY-1")
                {
                    found = true;
                    var flags = registryBuffer[i].Flags;
                    Assert.IsTrue((flags & Space4XRegistryFlags.ColonySupplyStrained) != 0,
                        "Colony should have SupplyStrained flag");
                    break;
                }
            }
            Assert.IsTrue(found, "Colony should be found in registry");
        }

        [Test]
        public void ColonyCriticalSupply_ReflectedInFlags()
        {
            // Create colony with critical supply shortage
            float population = 100000f;
            float demand = Space4XColonySupply.ComputeDemand(population);
            float criticalStoredResources = demand * 0.2f; // 20% of demand (below critical threshold)

            var colony = CreateColony("COLONY-1", population, criticalStoredResources, Space4XColonyStatus.Growing, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify flags
            var colonyRegistryEntity = FindColonyRegistryEntity();
            var registryBuffer = _entityManager.GetBuffer<Space4XColonyRegistryEntry>(colonyRegistryEntity);

            bool found = false;
            for (int i = 0; i < registryBuffer.Length; i++)
            {
                if (registryBuffer[i].ColonyId.ToString() == "COLONY-1")
                {
                    found = true;
                    var flags = registryBuffer[i].Flags;
                    Assert.IsTrue((flags & Space4XRegistryFlags.ColonySupplyCritical) != 0,
                        "Colony should have SupplyCritical flag");
                    Assert.IsTrue((flags & Space4XRegistryFlags.ColonySupplyStrained) != 0,
                        "Colony should also have SupplyStrained flag");
                    break;
                }
            }
            Assert.IsTrue(found, "Colony should be found in registry");
        }

        [Test]
        public void ColonySupplyShortage_CalculatedCorrectly()
        {
            // Create colony with supply shortage
            float population = 100000f;
            float demand = Space4XColonySupply.ComputeDemand(population);
            float storedResources = demand * 0.7f; // 70% of demand

            var colony = CreateColony("COLONY-1", population, storedResources, Space4XColonyStatus.Growing, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify shortage calculation
            float expectedShortage = Space4XColonySupply.ComputeShortage(storedResources, demand);
            Assert.Greater(expectedShortage, 0f, "Shortage should be positive");
            Assert.AreEqual(demand - storedResources, expectedShortage, 0.01f, "Shortage should be demand minus stored");
        }

        [Test]
        public void MultipleColonies_SupplyMetricsAggregated()
        {
            // Create multiple colonies with different supply levels
            float population1 = 100000f;
            float population2 = 200000f;
            float population3 = 50000f;

            var colony1 = CreateColony("COLONY-1", population1, 500f, Space4XColonyStatus.Growing, new float3(0f, 0f, 0f));
            var colony2 = CreateColony("COLONY-2", population2, 1000f, Space4XColonyStatus.Growing, new float3(10f, 0f, 10f));
            var colony3 = CreateColony("COLONY-3", population3, 200f, Space4XColonyStatus.Growing, new float3(-10f, 0f, -10f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify aggregated metrics
            var colonyRegistryEntity = FindColonyRegistryEntity();
            var summary = _entityManager.GetComponentData<Space4XColonyRegistry>(colonyRegistryEntity);

            Assert.AreEqual(3, summary.ColonyCount, "Colony count should be 3");
            Assert.AreEqual(population1 + population2 + population3, summary.TotalPopulation, 0.01f,
                "Total population should be sum");
            Assert.AreEqual(1700f, summary.TotalStoredResources, 0.01f, "Total stored resources should be sum");
        }

        [Test]
        public void ColonyRegistrySnapshot_Updated()
        {
            // Create colony
            var colony = CreateColony("COLONY-1", 100000f, 500f, Space4XColonyStatus.Growing, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify snapshot updated
            var snapshotEntity = FindSnapshotEntity();
            if (snapshotEntity != Entity.Null)
            {
                var snapshot = _entityManager.GetComponentData<Space4XRegistrySnapshot>(snapshotEntity);
                Assert.GreaterOrEqual(snapshot.ColonyCount, 1, "Snapshot should have colony count");
                Assert.GreaterOrEqual(snapshot.ColonySupplyDemandTotal, 0f, "Snapshot should have supply demand total");
            }
        }

        private Entity CreateColony(string colonyId, float population, float storedResources, Space4XColonyStatus status, float3 position)
        {
            var entity = _entityManager.CreateEntity(typeof(Space4XColony), typeof(LocalTransform), typeof(SpatialIndexedTag));
            _entityManager.SetComponentData(entity, new Space4XColony
            {
                ColonyId = new FixedString64Bytes(colonyId),
                Population = population,
                StoredResources = storedResources,
                Status = status,
                SectorId = 1
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        private void EnsureRegistryDirectory()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryDirectory>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = _entityManager.CreateEntity(typeof(RegistryDirectory));
                _entityManager.AddComponent<RegistryMetadata>(entity);
            }

            // Ensure MiracleRegistry exists
            using var miracleQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<MiracleRegistry>());
            if (miracleQuery.IsEmptyIgnoreFilter)
            {
                var entity = _entityManager.CreateEntity(typeof(MiracleRegistry));
                _entityManager.SetComponentData(entity, new MiracleRegistry
                {
                    TotalMiracles = 0,
                    ActiveMiracles = 0,
                    TotalEnergyCost = 0f,
                    TotalCooldownSeconds = 0f
                });
            }
        }

        private Entity FindColonyRegistryEntity()
        {
            using var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XColonyRegistry>(),
                ComponentType.ReadOnly<Space4XColonyRegistryEntry>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }
            return Entity.Null;
        }

        private Entity FindSnapshotEntity()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XRegistrySnapshot>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }
            return Entity.Null;
        }

        private void UpdateSystem(SystemHandle handle)
        {
            handle.Update(_world.Unmanaged);
        }
    }
}

