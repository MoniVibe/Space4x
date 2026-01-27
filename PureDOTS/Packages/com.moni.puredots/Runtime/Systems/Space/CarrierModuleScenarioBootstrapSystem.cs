using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Space
{
    /// <summary>
    /// Minimal bootstrap that seeds a carrier with modules for the ScenarioRunner sample
    /// when scenarioId == "scenario.space4x.modules.smoke".
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CarrierModuleScenarioBootstrapSystem : SystemBase
    {
        private static readonly FixedString64Bytes TargetScenarioId = new FixedString64Bytes("scenario.space4x.modules.smoke");

        protected override void OnCreate()
        {
            RequireForUpdate<ScenarioInfo>();
        }

        protected override void OnUpdate()
        {
            var scenarioInfo = SystemAPI.GetSingleton<ScenarioInfo>();
            if (!scenarioInfo.ScenarioId.Equals(TargetScenarioId))
            {
                Enabled = false;
                return;
            }

            SeedSampleCarrier();
            Enabled = false;
        }

        private void SeedSampleCarrier()
        {
            var carrier = EntityManager.CreateEntity(typeof(LocalTransform), typeof(CarrierRefitState),
                typeof(CarrierModuleStatTotals), typeof(CarrierPowerBudget));

            EntityManager.SetComponentData(carrier, LocalTransform.Identity);
            EntityManager.SetComponentData(carrier, new CarrierRefitState
            {
                FieldRefitRate = 4f,
                StationRefitRate = 8f,
                AtRefitFacility = true
            });
            EntityManager.SetComponentData(carrier, new CarrierPowerBudget
            {
                MaxPowerOutput = 8f,
                CurrentDraw = 0f,
                CurrentGeneration = 0f,
                OverBudget = false
            });

            var slots = EntityManager.AddBuffer<CarrierModuleSlot>(carrier);
            EntityManager.AddBuffer<ModuleRepairTicket>(carrier);
            EntityManager.AddBuffer<CarrierModuleRefitRequest>(carrier);

            var engine = CreateModule(carrier, new ShipModule
            {
                Family = ModuleFamily.Utility,
                Class = ModuleClass.Engine,
                RequiredMount = MountType.UtilityBay,
                RequiredSize = MountSize.Medium,
                ModuleName = "Engine Mk1",
                Mass = 8f,
                PowerRequired = 0f,
                PowerGeneration = 6f,
                EfficiencyPercent = 100,
                State = ModuleState.Active
            }, new ModuleStatModifier
            {
                Mass = 8f,
                PowerDraw = 0f,
                PowerGeneration = 6f
            }, new ModuleHealth
            {
                Integrity = 100,
                FailureThreshold = 25,
                RepairPriority = 2
            }, new ModuleDegradation
            {
                PassivePerSecond = 0.02f,
                ActivePerSecond = 0.08f,
                CombatMultiplier = 1.5f
            });

            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                Type = MountType.UtilityBay,
                Size = MountSize.Medium,
                InstalledModule = engine
            });

            var shield = CreateModule(carrier, new ShipModule
            {
                Family = ModuleFamily.Defense,
                Class = ModuleClass.Shield,
                RequiredMount = MountType.UtilityBay,
                RequiredSize = MountSize.Medium,
                ModuleName = "Shield Emitter",
                Mass = 12f,
                PowerRequired = 4f,
                PowerGeneration = 0f,
                EfficiencyPercent = 85,
                State = ModuleState.Active
            }, new ModuleStatModifier
            {
                Mass = 12f,
                PowerDraw = 4f,
                Shield = 10f,
                RepairRateBonus = 0.5f
            }, new ModuleHealth
            {
                Integrity = 55,
                FailureThreshold = 60,
                RepairPriority = 4
            }, new ModuleDegradation
            {
                PassivePerSecond = 0.05f,
                ActivePerSecond = 0.12f,
                CombatMultiplier = 1.8f
            });

            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 1,
                Type = MountType.UtilityBay,
                Size = MountSize.Medium,
                InstalledModule = shield
            });

            var miner = CreateModule(carrier, new ShipModule
            {
                Family = ModuleFamily.Utility,
                Class = ModuleClass.Mining,
                RequiredMount = MountType.UtilityBay,
                RequiredSize = MountSize.Small,
                ModuleName = "Mining Laser",
                Mass = 6f,
                PowerRequired = 2f,
                PowerGeneration = 0f,
                EfficiencyPercent = 75,
                State = ModuleState.Active
            }, new ModuleStatModifier
            {
                Mass = 6f,
                PowerDraw = 2f,
                MiningRate = 3f
            }, new ModuleHealth
            {
                Integrity = 20,
                FailureThreshold = 40,
                RepairPriority = 6,
                Flags = ModuleHealth.FlagRequiresRepair
            }, new ModuleDegradation
            {
                PassivePerSecond = 0.08f,
                ActivePerSecond = 0.2f,
                CombatMultiplier = 1.2f
            });

            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 2,
                Type = MountType.UtilityBay,
                Size = MountSize.Small,
                InstalledModule = miner
            });

            // Queue a station refit to replace the shield with a lighter variant once budget allows.
            var upgradePrefab = EntityManager.CreateEntity(typeof(ShipModule), typeof(ModuleStatModifier), typeof(ModuleHealth), typeof(ModuleDegradation));
            EntityManager.AddComponent<Prefab>(upgradePrefab);
            EntityManager.SetComponentData(upgradePrefab, new ShipModule
            {
                Family = ModuleFamily.Defense,
                Class = ModuleClass.Shield,
                RequiredMount = MountType.UtilityBay,
                RequiredSize = MountSize.Medium,
                ModuleName = "Shield Emitter Mk2",
                Mass = 10f,
                PowerRequired = 2f,
                PowerGeneration = 0f,
                EfficiencyPercent = 95,
                State = ModuleState.Standby
            });
            EntityManager.SetComponentData(upgradePrefab, new ModuleStatModifier
            {
                Mass = 10f,
                PowerDraw = 2f,
                Shield = 12f,
                RepairRateBonus = 1f
            });
            EntityManager.SetComponentData(upgradePrefab, new ModuleHealth
            {
                Integrity = 100,
                FailureThreshold = 60,
                RepairPriority = 5
            });
            EntityManager.SetComponentData(upgradePrefab, new ModuleDegradation
            {
                PassivePerSecond = 0.04f,
                ActivePerSecond = 0.1f,
                CombatMultiplier = 1.5f
            });

            var refitRequests = EntityManager.GetBuffer<CarrierModuleRefitRequest>(carrier);
            refitRequests.Add(new CarrierModuleRefitRequest
            {
                SlotIndex = 1,
                ExistingModule = shield,
                NewModulePrefab = upgradePrefab,
                WorkRemaining = 2f,
                RequiresStation = true
            });
        }

        private Entity CreateModule(Entity carrier, ShipModule module, ModuleStatModifier modifier, ModuleHealth health,
            ModuleDegradation degradation)
        {
            var entity = EntityManager.CreateEntity(typeof(Parent), typeof(ShipModule), typeof(ModuleStatModifier),
                typeof(ModuleHealth), typeof(ModuleDegradation));

            EntityManager.SetComponentData(entity, new Parent { Value = carrier });
            EntityManager.SetComponentData(entity, module);
            EntityManager.SetComponentData(entity, modifier);
            EntityManager.SetComponentData(entity, health);
            EntityManager.SetComponentData(entity, degradation);

            return entity;
        }
    }
}
