#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry.Tests
{
    public class Space4XRefitScenarioSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XRefitScenarioSystemTests");
            _entityManager = _world.EntityManager;

            EnsureTimeState();
            EnsureCatalogs();
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
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
        public void ScenarioSystemCreatesCatalogsWhenMissing()
        {
            _world.GetOrCreateSystem<ModuleCatalogBootstrapSystem>();
            _world.Update();

            Assert.IsTrue(_entityManager.CreateEntityQuery(ComponentType.ReadOnly<ModuleCatalogSingleton>()).CalculateEntityCount() > 0);
            Assert.IsTrue(_entityManager.CreateEntityQuery(ComponentType.ReadOnly<HullCatalogSingleton>()).CalculateEntityCount() > 0);
            Assert.IsTrue(_entityManager.CreateEntityQuery(ComponentType.ReadOnly<RefitRepairTuningSingleton>()).CalculateEntityCount() > 0);
        }

        [Test]
        public void ModuleCatalogUtilityCanLookupModuleSpecs()
        {
            EnsureCatalogs();
            var reactorId = new FixedString64Bytes("reactor-mk1");
            Assert.IsTrue(ModuleCatalogUtility.TryGetModuleSpec(_entityManager, reactorId, out var spec));
            Assert.AreEqual(reactorId, spec.Id);
            Assert.AreEqual(ModuleClass.Reactor, spec.Class);
            Assert.AreEqual(MountType.Core, spec.RequiredMount);
            Assert.AreEqual(MountSize.S, spec.RequiredSize);
            Assert.AreEqual(-120f, spec.PowerDrawMW);
        }

        [Test]
        public void ModuleCatalogUtilityCanLookupHullSpecs()
        {
            EnsureCatalogs();
            var hullId = new FixedString64Bytes("lcv-sparrow");
            Assert.IsTrue(ModuleCatalogUtility.TryGetHullSpec(_entityManager, hullId, out var catalogRef, out var hullIndex));
            ref var hulls = ref catalogRef.Value.Hulls;
            ref var hullSpec = ref hulls[hullIndex];
            Assert.AreEqual(hullId, hullSpec.Id);
            Assert.AreEqual(300f, hullSpec.BaseMassTons);
            Assert.IsTrue(hullSpec.FieldRefitAllowed);
            Assert.AreEqual(7, hullSpec.Slots.Length);
        }

        [Test]
        public void ModuleCatalogUtilityCanLookupTuning()
        {
            EnsureCatalogs();
            Assert.IsTrue(ModuleCatalogUtility.TryGetTuning(_entityManager, out var tuning));
            Assert.AreEqual(60f, tuning.BaseRefitSeconds);
            Assert.AreEqual(1.5f, tuning.MassSecPerTon);
            Assert.IsTrue(tuning.GlobalFieldRefitEnabled);
            Assert.AreEqual(0.01f, tuning.RepairRateEffPerSecStation);
            Assert.AreEqual(0.005f, tuning.RepairRateEffPerSecField);
        }

        [Test]
        public void RefitTimeCalculationUsesFormula()
        {
            EnsureCatalogs();
            Assert.IsTrue(ModuleCatalogUtility.TryGetTuning(_entityManager, out var tuning));
            Assert.IsTrue(ModuleCatalogUtility.TryGetModuleSpec(_entityManager, new FixedString64Bytes("laser-s-1"), out var moduleSpec));
            
            var fieldTime = ModuleCatalogUtility.CalculateRefitTime(tuning, moduleSpec, false, false);
            var facilityTime = ModuleCatalogUtility.CalculateRefitTime(tuning, moduleSpec, true, false);
            
            Assert.Greater(fieldTime, 0f);
            Assert.Less(facilityTime, fieldTime);
            
            var expectedField = tuning.BaseRefitSeconds + tuning.MassSecPerTon * moduleSpec.MassTons * tuning.SizeMultS * tuning.FieldTimeMult;
            Assert.AreEqual(expectedField, fieldTime, 0.01f);
        }

        [Test]
        public void FacilityProximitySystemAddsTagWhenInRange()
        {
            EnsureCatalogs();
            
            var carrierEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(carrierEntity, LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 0f), quaternion.identity, 1f));
            _entityManager.AddComponent<Carrier>(carrierEntity);
            
            var facilityEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(facilityEntity, LocalTransform.FromPositionRotationScale(new float3(5f, 0f, 0f), quaternion.identity, 1f));
            _entityManager.AddComponent<RefitFacilityTag>(facilityEntity);
            _entityManager.AddComponentData(facilityEntity, new FacilityZone { RadiusMeters = 10f });
            
            var proximityHandle = _world.GetOrCreateSystem<FacilityProximitySystem>();
            _world.Update();
            
            Assert.IsTrue(_entityManager.HasComponent<InRefitFacilityTag>(carrierEntity));
        }

        [Test]
        public void FacilityProximitySystemRemovesTagWhenOutOfRange()
        {
            EnsureCatalogs();
            
            var carrierEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(carrierEntity, LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 0f), quaternion.identity, 1f));
            _entityManager.AddComponent<Carrier>(carrierEntity);
            _entityManager.AddComponent<InRefitFacilityTag>(carrierEntity);
            
            var facilityEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(facilityEntity, LocalTransform.FromPositionRotationScale(new float3(50f, 0f, 0f), quaternion.identity, 1f));
            _entityManager.AddComponent<RefitFacilityTag>(facilityEntity);
            _entityManager.AddComponentData(facilityEntity, new FacilityZone { RadiusMeters = 10f });
            
            var proximityHandle = _world.GetOrCreateSystem<FacilityProximitySystem>();
            _world.Update();
            
            Assert.IsFalse(_entityManager.HasComponent<InRefitFacilityTag>(carrierEntity));
        }

        private void EnsureTimeState()
        {
            var timeEntity = _entityManager.CreateEntity(typeof(TimeState));
            _entityManager.SetComponentData(timeEntity, new TimeState
            {
                Tick = 0,
                FixedDeltaTime = 1f / 60f,
                CurrentSpeedMultiplier = 1f,
                IsPaused = false
            });
        }

        private void EnsureCatalogs()
        {
            var bootstrapHandle = _world.GetOrCreateSystem<ModuleCatalogBootstrapSystem>();
            _world.Update();
        }
    }
}
#endif
