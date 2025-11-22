using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XModuleSystemsTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XModuleSystemsTests");
            _entityManager = _world.EntityManager;
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
        public void ComponentDegradationAppliesHazardDamageAndMarksFailure()
        {
            SetTimeAndRewind(1f, RewindMode.Record);

            var module = _entityManager.CreateEntity(typeof(ModuleHealth));
            _entityManager.SetComponentData(module, new ModuleHealth
            {
                CurrentHealth = 2f,
                MaxHealth = 5f,
                MaxFieldRepairHealth = 4f,
                DegradationPerSecond = 0.5f,
                RepairPriority = 0,
                Failed = 0
            });

            var hazardEvents = _entityManager.AddBuffer<HazardDamageEvent>(module);
            hazardEvents.Add(new HazardDamageEvent
            {
                HazardType = HazardTypeId.Radiation,
                Amount = 2f
            });

            var system = _world.GetOrCreateSystem<Space4XComponentDegradationSystem>();
            system.Update(_world.Unmanaged);

            var health = _entityManager.GetComponentData<ModuleHealth>(module);
            Assert.AreEqual(0f, health.CurrentHealth, 1e-3f);
            Assert.AreEqual(1, health.Failed);

            var clearedEvents = _entityManager.GetBuffer<HazardDamageEvent>(module);
            Assert.AreEqual(0, clearedEvents.Length);
        }

        [Test]
        public void FieldRepairRespectsPriorityAndFieldCap()
        {
            SetTimeAndRewind(1f, RewindMode.Record);

            var carrier = _entityManager.CreateEntity(typeof(FieldRepairCapability));
            _entityManager.SetComponentData(carrier, new FieldRepairCapability
            {
                RepairRatePerSecond = 1f,
                CriticalRepairRate = 0.5f,
                CanRepairCritical = 0
            });

            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(carrier);

            var criticalModule = _entityManager.CreateEntity(typeof(ModuleHealth));
            _entityManager.SetComponentData(criticalModule, new ModuleHealth
            {
                CurrentHealth = 0f,
                MaxHealth = 1f,
                MaxFieldRepairHealth = 0.75f,
                DegradationPerSecond = 0f,
                RepairPriority = 0,
                Failed = 0
            });

            var repairableModule = _entityManager.CreateEntity(typeof(ModuleHealth));
            _entityManager.SetComponentData(repairableModule, new ModuleHealth
            {
                CurrentHealth = 0.5f,
                MaxHealth = 1f,
                MaxFieldRepairHealth = 0.8f,
                DegradationPerSecond = 0f,
                RepairPriority = 1,
                Failed = 0
            });

            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                SlotSize = ModuleSlotSize.Medium,
                CurrentModule = criticalModule,
                TargetModule = Entity.Null,
                RefitProgress = 0f,
                State = ModuleSlotState.Active
            });

            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 1,
                SlotSize = ModuleSlotSize.Medium,
                CurrentModule = repairableModule,
                TargetModule = Entity.Null,
                RefitProgress = 0f,
                State = ModuleSlotState.Active
            });

            var system = _world.GetOrCreateSystem<Space4XFieldRepairSystem>();
            system.Update(_world.Unmanaged);

            var updatedRepairable = _entityManager.GetComponentData<ModuleHealth>(repairableModule);
            Assert.AreEqual(0.8f, updatedRepairable.CurrentHealth, 1e-3f);
            Assert.AreEqual(0, updatedRepairable.Failed);

            var updatedCritical = _entityManager.GetComponentData<ModuleHealth>(criticalModule);
            Assert.AreEqual(0f, updatedCritical.CurrentHealth, 1e-3f);

            Assert.IsTrue(_entityManager.HasComponent<CrewSkills>(carrier));
            Assert.IsTrue(_entityManager.HasComponent<SkillExperienceGain>(carrier));
            var xp = _entityManager.GetComponentData<SkillExperienceGain>(carrier);
            Assert.Greater(xp.RepairXp, 0f);
        }

        [Test]
        public void CarrierModuleRefitProcessesRequestsAndStoresProgress()
        {
            SetTimeAndRewind(0.5f, RewindMode.Record);

            var targetModule = _entityManager.CreateEntity(typeof(ModuleSlotRequirement));
            _entityManager.SetComponentData(targetModule, new ModuleSlotRequirement
            {
                SlotSize = ModuleSlotSize.Medium
            });

            var carrier = _entityManager.CreateEntity(typeof(ModuleRefitFacility));
            _entityManager.SetComponentData(carrier, new ModuleRefitFacility
            {
                RefitRatePerSecond = 1f,
                SupportsFieldRefit = 1
            });

            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(carrier);
            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                SlotSize = ModuleSlotSize.Medium,
                CurrentModule = Entity.Null,
                TargetModule = Entity.Null,
                RefitProgress = 0f,
                State = ModuleSlotState.Empty
            });

            var requests = _entityManager.AddBuffer<ModuleRefitRequest>(carrier);
            requests.Add(new ModuleRefitRequest
            {
                SlotIndex = 0,
                TargetModule = targetModule,
                Priority = 0,
                RequestTick = 1,
                RequiredWork = 1f
            });

            var system = _world.GetOrCreateSystem<Space4XCarrierModuleRefitSystem>();

            system.Update(_world.Unmanaged);

            slots = _entityManager.GetBuffer<CarrierModuleSlot>(carrier);
            requests = _entityManager.GetBuffer<ModuleRefitRequest>(carrier);
            Assert.AreEqual(1, requests.Length, "Refit request should remain until required work completes.");
            Assert.AreEqual(ModuleSlotState.Installing, slots[0].State);
            Assert.AreEqual(targetModule, slots[0].TargetModule);
            Assert.Greater(slots[0].RefitProgress, 0f);

            system.Update(_world.Unmanaged);

            slots = _entityManager.GetBuffer<CarrierModuleSlot>(carrier);
            requests = _entityManager.GetBuffer<ModuleRefitRequest>(carrier);
            Assert.AreEqual(0, requests.Length, "Refit request should be removed after completion.");
            Assert.AreEqual(ModuleSlotState.Active, slots[0].State);
            Assert.AreEqual(targetModule, slots[0].CurrentModule);
            Assert.AreEqual(0f, slots[0].RefitProgress, 1e-5f);

            Assert.IsTrue(_entityManager.HasComponent<CrewSkills>(carrier));
            Assert.IsTrue(_entityManager.HasComponent<SkillExperienceGain>(carrier));
            var xp = _entityManager.GetComponentData<SkillExperienceGain>(carrier);
            Assert.Greater(xp.RepairXp, 0f);
        }

        private void SetTimeAndRewind(float fixedDeltaTime, RewindMode mode)
        {
            var timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var timeState = _entityManager.GetComponentData<TimeState>(timeEntity);
            timeState.FixedDeltaTime = fixedDeltaTime;
            timeState.Tick = 0;
            timeState.IsPaused = false;
            _entityManager.SetComponentData(timeEntity, timeState);
            _entityManager.SetComponentData(timeEntity, new GameplayFixedStep
            {
                FixedDeltaTime = fixedDeltaTime
            });

            var rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();
            var rewindState = _entityManager.GetComponentData<RewindState>(rewindEntity);
            rewindState.Mode = mode;
            _entityManager.SetComponentData(rewindEntity, rewindState);
        }
    }
}
