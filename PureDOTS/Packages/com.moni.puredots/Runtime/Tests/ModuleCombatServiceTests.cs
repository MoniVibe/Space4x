#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Ships;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Tests.Combat
{
    /// <summary>
    /// Unit tests for module combat services.
    /// </summary>
    public class ModuleCombatServiceTests
    {
        private World _world;
        private EntityManager _entityManager;
        private EntityManagerLookupBootstrapSystem _lookupSystem;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ModuleCombatServiceTests");
            _entityManager = _world.EntityManager;
            _lookupSystem = _world.GetOrCreateSystemManaged<EntityManagerLookupBootstrapSystem>();
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
        public void ModuleTargetingService_SelectModuleTarget_SelectsHighestPriorityModule()
        {
            // Create ship with multiple modules
            var ship = _entityManager.CreateEntity();
            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(ship);

            // Create modules with different priorities
            var engineModule = CreateModule(ModuleClass.Engine, 200); // Highest priority
            var weaponModule = CreateModule(ModuleClass.BeamCannon, 150);
            var cargoModule = CreateModule(ModuleClass.Cargo, 50); // Lowest priority

            slots.Add(new CarrierModuleSlot { Type = MountType.UtilityBay, Size = MountSize.Large, InstalledModule = engineModule, Priority = 0 });
            slots.Add(new CarrierModuleSlot { Type = MountType.MainGun, Size = MountSize.Medium, InstalledModule = weaponModule, Priority = 0 });
            slots.Add(new CarrierModuleSlot { Type = MountType.UtilityBay, Size = MountSize.Small, InstalledModule = cargoModule, Priority = 0 });

            var attacker = _entityManager.CreateEntity();

            // Test module targeting
            var entityLookup = _entityManager.GetEntityStorageInfoLookup();
            var slotLookup = _entityManager.GetBufferLookup<CarrierModuleSlot>(true);
            var moduleLookup = _entityManager.GetComponentLookup<ShipModule>(true);
            var priorityLookup = _entityManager.GetComponentLookup<ModuleTargetPriority>(true);
            var healthLookup = _entityManager.GetComponentLookup<ModuleHealth>(true);

            entityLookup.Update(_lookupSystem);
            slotLookup.Update(_lookupSystem);
            moduleLookup.Update(_lookupSystem);
            priorityLookup.Update(_lookupSystem);
            healthLookup.Update(_lookupSystem);

            var selectedModule = ModuleTargetingService.SelectModuleTarget(
                entityLookup, slotLookup, moduleLookup, priorityLookup, healthLookup,
                attacker, ship);

            // Should select engine (highest priority)
            Assert.AreEqual(engineModule, selectedModule);
        }

        [Test]
        public void ModuleTargetingService_SelectModuleTarget_IgnoresDestroyedModules()
        {
            var ship = _entityManager.CreateEntity();
            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(ship);

            var engineModule = CreateModule(ModuleClass.Engine, 200);
            var weaponModule = CreateModule(ModuleClass.BeamCannon, 150);

            // Mark engine as destroyed
            var engineHealth = _entityManager.GetComponentData<ModuleHealth>(engineModule);
            engineHealth.Health = 0f;
            engineHealth.State = ModuleHealthState.Destroyed;
            _entityManager.SetComponentData(engineModule, engineHealth);

            slots.Add(new CarrierModuleSlot { Type = MountType.UtilityBay, Size = MountSize.Large, InstalledModule = engineModule, Priority = 0 });
            slots.Add(new CarrierModuleSlot { Type = MountType.MainGun, Size = MountSize.Medium, InstalledModule = weaponModule, Priority = 0 });

            var attacker = _entityManager.CreateEntity();

            var entityLookup = _entityManager.GetEntityStorageInfoLookup();
            var slotLookup = _entityManager.GetBufferLookup<CarrierModuleSlot>(true);
            var moduleLookup = _entityManager.GetComponentLookup<ShipModule>(true);
            var priorityLookup = _entityManager.GetComponentLookup<ModuleTargetPriority>(true);
            var healthLookup = _entityManager.GetComponentLookup<ModuleHealth>(true);

            entityLookup.Update(_lookupSystem);
            slotLookup.Update(_lookupSystem);
            moduleLookup.Update(_lookupSystem);
            priorityLookup.Update(_lookupSystem);
            healthLookup.Update(_lookupSystem);

            var selectedModule = ModuleTargetingService.SelectModuleTarget(
                entityLookup, slotLookup, moduleLookup, priorityLookup, healthLookup,
                attacker, ship);

            // Should select weapon (engine is destroyed)
            Assert.AreEqual(weaponModule, selectedModule);
        }

        [Test]
        public void ModuleDamageRouterService_RouteDamageToModule_ReducesModuleHealth()
        {
            var ship = _entityManager.CreateEntity();
            var module = CreateModule(ModuleClass.BeamCannon, 150);

            var entityLookup = _entityManager.GetEntityStorageInfoLookup();
            var healthLookup = _entityManager.GetComponentLookup<ModuleHealth>(false);
            var moduleLookup = _entityManager.GetComponentLookup<ShipModule>(false);

            entityLookup.Update(_lookupSystem);
            healthLookup.Update(_lookupSystem);
            moduleLookup.Update(_lookupSystem);

            var initialHealth = healthLookup[module].Health;
            float damageAmount = 30f;

            ModuleDamageRouterService.RouteDamageToModule(
                entityLookup, healthLookup, moduleLookup, ship, module, damageAmount);

            var finalHealth = healthLookup[module].Health;
            Assert.AreEqual(initialHealth - damageAmount, finalHealth, 0.01f);
        }

        [Test]
        public void ModuleDamageRouterService_RouteDamageToModule_MarksModuleAsDestroyedWhenHealthZero()
        {
            var ship = _entityManager.CreateEntity();
            var module = CreateModule(ModuleClass.BeamCannon, 150);

            var entityLookup = _entityManager.GetEntityStorageInfoLookup();
            var healthLookup = _entityManager.GetComponentLookup<ModuleHealth>(false);
            var moduleLookup = _entityManager.GetComponentLookup<ShipModule>(false);

            entityLookup.Update(_lookupSystem);
            healthLookup.Update(_lookupSystem);
            moduleLookup.Update(_lookupSystem);

            float damageAmount = 150f; // More than max health

            ModuleDamageRouterService.RouteDamageToModule(
                entityLookup, healthLookup, moduleLookup, ship, module, damageAmount);

            var health = healthLookup[module];
            var shipModule = moduleLookup[module];

            Assert.AreEqual(0f, health.Health, 0.01f);
            Assert.AreEqual(ModuleHealthState.Destroyed, health.State);
            Assert.AreEqual(ModuleState.Destroyed, shipModule.State);
        }

        [Test]
        public void CapabilityDisableService_UpdateCapabilitiesFromModules_DisablesMovementWhenEnginesDestroyed()
        {
            var ship = _entityManager.CreateEntity();
            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(ship);

            var engineModule = CreateModule(ModuleClass.Engine, 200);
            
            // Destroy engine
            var engineHealth = _entityManager.GetComponentData<ModuleHealth>(engineModule);
            engineHealth.Health = 0f;
            engineHealth.State = ModuleHealthState.Destroyed;
            _entityManager.SetComponentData(engineModule, engineHealth);

            var engineShipModule = _entityManager.GetComponentData<ShipModule>(engineModule);
            engineShipModule.State = ModuleState.Destroyed;
            _entityManager.SetComponentData(engineModule, engineShipModule);

            slots.Add(new CarrierModuleSlot { Type = MountType.UtilityBay, Size = MountSize.Large, InstalledModule = engineModule, Priority = 0 });

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var entityLookup = _entityManager.GetEntityStorageInfoLookup();
            var slotLookup = _entityManager.GetBufferLookup<CarrierModuleSlot>(true);
            var moduleLookup = _entityManager.GetComponentLookup<ShipModule>(true);
            var healthLookup = _entityManager.GetComponentLookup<ModuleHealth>(true);
            var capabilityStateLookup = _entityManager.GetComponentLookup<CapabilityState>(false);
            var effectivenessLookup = _entityManager.GetComponentLookup<CapabilityEffectiveness>(false);

            entityLookup.Update(_lookupSystem);
            slotLookup.Update(_lookupSystem);
            moduleLookup.Update(_lookupSystem);
            healthLookup.Update(_lookupSystem);
            capabilityStateLookup.Update(_lookupSystem);
            effectivenessLookup.Update(_lookupSystem);

            // Initialize capability state
            if (!capabilityStateLookup.HasComponent(ship))
            {
                ecb.AddComponent(ship, new CapabilityState { EnabledCapabilities = CapabilityFlags.Movement | CapabilityFlags.Firing });
            }

            CapabilityDisableService.UpdateCapabilitiesFromModules(
                entityLookup, slotLookup, moduleLookup, healthLookup,
                capabilityStateLookup, effectivenessLookup, ecb, ship);

            ecb.Playback(_entityManager);
            ecb.Dispose();

            var capabilityState = capabilityStateLookup[ship];
            // Movement should be disabled (no engines)
            Assert.IsFalse((capabilityState.EnabledCapabilities & CapabilityFlags.Movement) != 0);
        }

        private Entity CreateModule(ModuleClass moduleClass, byte priority)
        {
            var module = _entityManager.CreateEntity();
            
            // Determine family from class
            ModuleFamily family = moduleClass switch
            {
                ModuleClass.BeamCannon or ModuleClass.MassDriver or ModuleClass.Missile or ModuleClass.PointDefense => ModuleFamily.Weapon,
                ModuleClass.Shield or ModuleClass.Armor => ModuleFamily.Defense,
                ModuleClass.Engine or ModuleClass.Sensor or ModuleClass.Cargo or ModuleClass.Hangar => ModuleFamily.Utility,
                ModuleClass.Fabrication or ModuleClass.Research or ModuleClass.Medical => ModuleFamily.Facility,
                _ => ModuleFamily.Utility
            };

            _entityManager.AddComponentData(module, new ShipModule
            {
                ModuleId = new Unity.Collections.FixedString64Bytes(moduleClass.ToString()),
                Family = family,
                Class = moduleClass,
                RequiredMount = MountType.UtilityBay,
                RequiredSize = MountSize.Medium,
                Mass = 10f,
                PowerRequired = moduleClass == ModuleClass.Engine ? 0f : 5f,
                OffenseRating = moduleClass == ModuleClass.BeamCannon ? 50f : 0f,
                DefenseRating = 0f,
                UtilityRating = moduleClass == ModuleClass.Engine ? 100f : 10f,
                EfficiencyPercent = 100,
                State = ModuleState.Active
            });

            _entityManager.AddComponentData(module, new ModuleHealth
            {
                MaxHealth = 100f,
                Health = 100f,
                DegradationPerTick = 0f,
                FailureThreshold = 25f,
                State = ModuleHealthState.Nominal,
                Flags = ModuleHealthFlags.None,
                LastProcessedTick = 0
            });

            _entityManager.AddComponentData(module, new ModuleTargetPriority { Priority = priority });

            _entityManager.AddComponentData(module, new ModulePosition
            {
                LocalPosition = float3.zero,
                Radius = 1.5f
            });

            return module;
        }
    }
}
#endif

