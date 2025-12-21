#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
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
            CoreSingletonBootstrapSystem.EnsureModuleMaintenanceTelemetry(_entityManager);
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

            var maintenanceEntity = GetMaintenanceEntity();
            var log = _entityManager.GetBuffer<ModuleMaintenanceCommandLogEntry>(maintenanceEntity);
            Assert.AreEqual(1, log.Length);
            Assert.AreEqual(ModuleMaintenanceEventType.ModuleFailed, log[0].EventType);
            Assert.AreEqual(module, log[0].Module);

            var telemetry = _entityManager.GetComponentData<ModuleMaintenanceTelemetry>(maintenanceEntity);
            Assert.AreEqual(1u, telemetry.Failures);
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

            var maintenanceEntity = GetMaintenanceEntity();
            var log = _entityManager.GetBuffer<ModuleMaintenanceCommandLogEntry>(maintenanceEntity);
            Assert.IsTrue(log.Length > 0);
            Assert.AreEqual(ModuleMaintenanceEventType.RepairApplied, log[0].EventType);
            Assert.AreEqual(carrier, log[0].Carrier);

            var telemetry = _entityManager.GetComponentData<ModuleMaintenanceTelemetry>(maintenanceEntity);
            Assert.Greater(telemetry.RepairApplied, 0f);
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

            var maintenanceEntity = GetMaintenanceEntity();
            var log = _entityManager.GetBuffer<ModuleMaintenanceCommandLogEntry>(maintenanceEntity);
            Assert.AreEqual(2, log.Length);
            Assert.AreEqual(ModuleMaintenanceEventType.RefitStarted, log[0].EventType);
            Assert.AreEqual(ModuleMaintenanceEventType.RefitCompleted, log[1].EventType);
            Assert.AreEqual(targetModule, log[1].Module);

            var telemetry = _entityManager.GetComponentData<ModuleMaintenanceTelemetry>(maintenanceEntity);
            Assert.AreEqual(1u, telemetry.RefitStarted);
            Assert.AreEqual(1u, telemetry.RefitCompleted);
        }

        [Test]
        public void RefitRequiresDockingWhenFacilityIsStationOnly()
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
                SupportsFieldRefit = 0
            });

            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(carrier);
            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                SlotSize = ModuleSlotSize.Medium,
                CurrentModule = Entity.Null,
                TargetModule = Entity.Null,
                State = ModuleSlotState.Empty
            });

            var requests = _entityManager.AddBuffer<ModuleRefitRequest>(carrier);
            requests.Add(new ModuleRefitRequest
            {
                SlotIndex = 0,
                TargetModule = targetModule,
                Priority = 0,
                RequestTick = 1,
                RequiredWork = 0.25f
            });

            var system = _world.GetOrCreateSystem<Space4XCarrierModuleRefitSystem>();
            system.Update(_world.Unmanaged);

            slots = _entityManager.GetBuffer<CarrierModuleSlot>(carrier);
            Assert.AreEqual(ModuleSlotState.Empty, slots[0].State);

            _entityManager.AddComponent<DockedAtStation>(carrier);
            system.Update(_world.Unmanaged);

            slots = _entityManager.GetBuffer<CarrierModuleSlot>(carrier);
            Assert.AreEqual(ModuleSlotState.Installing, slots[0].State);
        }

        [Test]
        public void StationOverhaulRepairsBeyondFieldCap()
        {
            SetTimeAndRewind(0.25f, RewindMode.Record);

            var carrier = _entityManager.CreateEntity(typeof(StationOverhaulFacility), typeof(ModuleStatAggregate));
            _entityManager.SetComponentData(carrier, new StationOverhaulFacility
            {
                OverhaulRatePerSecond = 4f
            });
            _entityManager.SetComponentData(carrier, new ModuleStatAggregate
            {
                SpeedMultiplier = 1f,
                CargoMultiplier = 1f,
                EnergyMultiplier = 1f,
                RefitRateMultiplier = 1f,
                RepairRateMultiplier = 1f
            });
            _entityManager.AddComponent<DockedAtStation>(carrier);

            var module = _entityManager.CreateEntity(typeof(ModuleHealth));
            _entityManager.SetComponentData(module, new ModuleHealth
            {
                CurrentHealth = 0.3f,
                MaxHealth = 1f,
                MaxFieldRepairHealth = 0.6f,
                DegradationPerSecond = 0f,
                RepairPriority = 5,
                Failed = 0
            });

            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(carrier);
            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                SlotSize = ModuleSlotSize.Medium,
                CurrentModule = module,
                State = ModuleSlotState.Active
            });

            var system = _world.GetOrCreateSystem<Space4XStationOverhaulSystem>();
            system.Update(_world.Unmanaged);

            var health = _entityManager.GetComponentData<ModuleHealth>(module);
            Assert.AreEqual(1f, health.CurrentHealth, 1e-3f);

            var telemetry = _entityManager.GetComponentData<ModuleMaintenanceTelemetry>(GetMaintenanceEntity());
            Assert.Greater(telemetry.RepairApplied, 0f);
        }

        [Test]
        public void MaintenancePlaybackRebuildsTelemetry()
        {
            SetTimeAndRewind(0.5f, RewindMode.Record);

            var logEntity = GetMaintenanceEntity();
            var log = _entityManager.GetBuffer<ModuleMaintenanceCommandLogEntry>(logEntity);
            log.Add(new ModuleMaintenanceCommandLogEntry
            {
                Tick = 5,
                Carrier = Entity.Null,
                SlotIndex = 0,
                Module = Entity.Null,
                EventType = ModuleMaintenanceEventType.RefitStarted,
                Amount = 1f
            });
            log.Add(new ModuleMaintenanceCommandLogEntry
            {
                Tick = 7,
                Carrier = Entity.Null,
                SlotIndex = 0,
                Module = Entity.Null,
                EventType = ModuleMaintenanceEventType.RefitCompleted,
                Amount = 1f
            });

            var playbackSystem = _world.GetOrCreateSystem<Space4XModuleMaintenancePlaybackSystem>();
            var timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();

            var rewindState = _entityManager.GetComponentData<RewindState>(rewindEntity);
            rewindState.Mode = RewindMode.Playback;
            rewindState.PlaybackTick = 6;
            _entityManager.SetComponentData(rewindEntity, rewindState);

            var timeState = _entityManager.GetComponentData<TimeState>(timeEntity);
            timeState.Tick = 10;
            _entityManager.SetComponentData(timeEntity, timeState);

            playbackSystem.Update(_world.Unmanaged);

            var telemetry = _entityManager.GetComponentData<ModuleMaintenanceTelemetry>(logEntity);
            Assert.AreEqual(1u, telemetry.RefitStarted);
            Assert.AreEqual(0u, telemetry.RefitCompleted);
            Assert.AreEqual(5u, telemetry.LastUpdateTick);
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

        private Entity GetMaintenanceEntity()
        {
            return _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ModuleMaintenanceLog>()).GetSingletonEntity();
        }
    }
}
#endif
