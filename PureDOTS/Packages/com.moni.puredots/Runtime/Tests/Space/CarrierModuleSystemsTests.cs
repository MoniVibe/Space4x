using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using PureDOTS.Systems.Space;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Tests.Space
{
    public class CarrierModuleSystemsTests
    {
        [Test]
        public void AggregationSumsModuleModifiersAndHealth()
        {
            using var world = new World("aggregation-world");
            var entityManager = world.EntityManager;
            SetupTimeSingletons(entityManager);

            var carrier = entityManager.CreateEntity(typeof(CarrierModuleStatTotals));
            var slots = entityManager.AddBuffer<CarrierModuleSlot>(carrier);

            var moduleA = entityManager.CreateEntity(typeof(ModuleStatModifier), typeof(ModuleHealth), typeof(ShipModule));
            entityManager.SetComponentData(moduleA, new ModuleStatModifier
            {
                Mass = 10f,
                PowerDraw = 2f,
                PowerGeneration = 1f,
                CargoCapacity = 5f,
                MiningRate = 2f,
                RepairRateBonus = 1f
            });
            entityManager.SetComponentData(moduleA, new ModuleHealth
            {
                Integrity = 100,
                FailureThreshold = 25,
                RepairPriority = 1
            });
            entityManager.SetComponentData(moduleA, new ShipModule
            {
                EfficiencyPercent = 100,
                State = ModuleState.Active
            });

            var moduleB = entityManager.CreateEntity(typeof(ModuleStatModifier), typeof(ModuleHealth), typeof(ShipModule));
            entityManager.SetComponentData(moduleB, new ModuleStatModifier
            {
                Mass = 5f,
                PowerDraw = 1f,
                PowerGeneration = 0f,
                CargoCapacity = 2f,
                MiningRate = 0f,
                RepairRateBonus = 0f
            });
            entityManager.SetComponentData(moduleB, new ModuleHealth
            {
                Integrity = 40,
                FailureThreshold = 50,
                RepairPriority = 2
            });
            entityManager.SetComponentData(moduleB, new ShipModule
            {
                EfficiencyPercent = 80,
                State = ModuleState.Damaged
            });

            slots.Add(new CarrierModuleSlot { SlotIndex = 0, Type = MountType.UtilityBay, Size = MountSize.Small, InstalledModule = moduleA });
            slots.Add(new CarrierModuleSlot { SlotIndex = 1, Type = MountType.UtilityBay, Size = MountSize.Small, InstalledModule = moduleB });

            var system = world.CreateSystemManaged<CarrierModuleStatAggregationSystem>();
            system.Update();

            var totals = entityManager.GetComponentData<CarrierModuleStatTotals>(carrier);
            Assert.AreEqual(15f, totals.TotalMass, 0.001f);
            Assert.AreEqual(2.4f, totals.TotalPowerDraw, 0.001f);
            Assert.AreEqual(1f, totals.TotalPowerGeneration, 0.001f);
            Assert.AreEqual(5.8f, totals.TotalCargoCapacity, 0.001f);
            Assert.AreEqual(2f, totals.TotalMiningRate, 0.001f);
            Assert.AreEqual(1f, totals.TotalRepairRateBonus, 0.001f);
            Assert.AreEqual(1, totals.DamagedModuleCount);
            Assert.AreEqual(0, totals.DestroyedModuleCount);
        }

        [Test]
        public void DegradationQueuesRepairWhenThresholdReached()
        {
            using var world = new World("degradation-world");
            var entityManager = world.EntityManager;
            SetupTimeSingletons(entityManager);

            var carrier = entityManager.CreateEntity();
            entityManager.AddBuffer<ModuleRepairTicket>(carrier);

            var module = entityManager.CreateEntity(typeof(ModuleHealth), typeof(ModuleDegradation), typeof(ShipModule), typeof(Parent));
            entityManager.SetComponentData(module, new ModuleHealth
            {
                Integrity = 20,
                FailureThreshold = 25,
                RepairPriority = 3
            });
            entityManager.SetComponentData(module, new ModuleDegradation
            {
                PassivePerSecond = 10f,
                ActivePerSecond = 0f,
                CombatMultiplier = 1f
            });
            entityManager.SetComponentData(module, new ShipModule { State = ModuleState.Active, EfficiencyPercent = 100 });
            entityManager.SetComponentData(module, new Parent { Value = carrier });

            var degradationSystem = world.CreateSystemManaged<ModuleDegradationSystem>();
            degradationSystem.Update();

            var health = entityManager.GetComponentData<ModuleHealth>(module);
            Assert.IsTrue(health.NeedsRepair);
            Assert.AreEqual(ModuleState.Damaged, entityManager.GetComponentData<ShipModule>(module).State);

            var buffer = entityManager.GetBuffer<ModuleRepairTicket>(carrier);
            Assert.AreEqual(1, buffer.Length);
            Assert.AreEqual(module, buffer[0].Module);
            Assert.Greater(buffer[0].RemainingWork, 0f);
        }

        [Test]
        public void RepairSystemRestoresIntegrityAndClearsTicket()
        {
            using var world = new World("repair-world");
            var entityManager = world.EntityManager;
            SetupTimeSingletons(entityManager);

            var carrier = entityManager.CreateEntity(typeof(CarrierRefitState));
            entityManager.SetComponentData(carrier, new CarrierRefitState
            {
                FieldRefitRate = 5f,
                StationRefitRate = 10f,
                AtRefitFacility = true
            });

            var tickets = entityManager.AddBuffer<ModuleRepairTicket>(carrier);

            var module = entityManager.CreateEntity(typeof(ModuleHealth), typeof(ShipModule));
            entityManager.SetComponentData(module, new ModuleHealth
            {
                Integrity = 10,
                FailureThreshold = 50,
                RepairPriority = 5,
                Flags = ModuleHealth.FlagRequiresRepair
            });
            entityManager.SetComponentData(module, new ShipModule { State = ModuleState.Damaged, EfficiencyPercent = 100 });

            tickets.Add(new ModuleRepairTicket
            {
                Module = module,
                Kind = ModuleRepairKind.Field,
                Priority = 5,
                RemainingWork = 0.5f
            });

            var repairSystem = world.CreateSystemManaged<CarrierModuleRepairSystem>();
            repairSystem.Update();

            var updatedHealth = entityManager.GetComponentData<ModuleHealth>(module);
            Assert.AreEqual(100, updatedHealth.Integrity);
            Assert.IsFalse(updatedHealth.NeedsRepair);
            Assert.AreEqual(ModuleState.Standby, entityManager.GetComponentData<ShipModule>(module).State);
            Assert.AreEqual(0, entityManager.GetBuffer<ModuleRepairTicket>(carrier).Length);
        }

        [Test]
        public void RefitSystemSwapsModulesWhenWorkCompletes()
        {
            using var world = new World("refit-world");
            var entityManager = world.EntityManager;
            SetupTimeSingletons(entityManager);

            var carrier = entityManager.CreateEntity(typeof(CarrierRefitState));
            entityManager.SetComponentData(carrier, new CarrierRefitState
            {
                FieldRefitRate = 2f,
                StationRefitRate = 0f,
                AtRefitFacility = false
            });

            var slots = entityManager.AddBuffer<CarrierModuleSlot>(carrier);

            var oldModule = entityManager.CreateEntity(typeof(ModuleStatModifier));
            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                Type = MountType.UtilityBay,
                Size = MountSize.Small,
                InstalledModule = oldModule
            });

            var prefab = entityManager.CreateEntity(typeof(ModuleStatModifier));
            entityManager.SetComponentData(prefab, new ModuleStatModifier { Mass = 1f });

            var requests = entityManager.AddBuffer<CarrierModuleRefitRequest>(carrier);
            requests.Add(new CarrierModuleRefitRequest
            {
                SlotIndex = 0,
                ExistingModule = oldModule,
                NewModulePrefab = prefab,
                WorkRemaining = 0.1f,
                RequiresStation = false
            });

            var refitSystem = world.CreateSystemManaged<CarrierModuleRefitSystem>();
            refitSystem.Update();

            var updatedSlots = entityManager.GetBuffer<CarrierModuleSlot>(carrier);
            Assert.AreNotEqual(oldModule, updatedSlots[0].InstalledModule);
            Assert.IsFalse(entityManager.Exists(oldModule));
            Assert.IsTrue(entityManager.HasComponent<Parent>(updatedSlots[0].InstalledModule));
            Assert.AreEqual(carrier, entityManager.GetComponentData<Parent>(updatedSlots[0].InstalledModule).Value);
            var updatedRequests = entityManager.GetBuffer<CarrierModuleRefitRequest>(carrier);
            Assert.AreEqual(0, updatedRequests.Length);
        }

        private static void SetupTimeSingletons(EntityManager entityManager)
        {
            var timeEntity = entityManager.CreateEntity(typeof(TimeState), typeof(RewindState), typeof(RewindLegacyState));
            entityManager.SetComponentData(timeEntity, new TimeState
            {
                FixedDeltaTime = 1f / 60f,
                DeltaTime = 1f / 60f,
                CurrentSpeedMultiplier = 1f,
                Tick = 1,
                IsPaused = false
            });
            entityManager.SetComponentData(timeEntity, new RewindState
            {
                Mode = RewindMode.Record,
                TargetTick = 0,
                TickDuration = 1f / 60f,
                MaxHistoryTicks = 600,
                PendingStepTicks = 0
            });
            entityManager.SetComponentData(timeEntity, new RewindLegacyState
            {
                PlaybackSpeed = 1f,
                CurrentTick = 0,
                StartTick = 0,
                PlaybackTick = 0,
                PlaybackTicksPerSecond = 1f,
                ScrubDirection = ScrubDirection.Forward,
                ScrubSpeedMultiplier = 1f,
                RewindWindowTicks = 0,
                ActiveTrack = default
            });
        }
    }
}
