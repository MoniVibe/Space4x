using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using CombatHitEvent = PureDOTS.Runtime.Combat.HitEvent;

namespace PureDOTS.Systems.Space
{
    /// <summary>
    /// Bootstrap system that spawns test carriers with modules for module combat smoke test.
    /// Handles scenario.space4x.module.combat.smoke scenario ID.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ModuleCombatScenarioBootstrapSystem : SystemBase
    {
        private static readonly FixedString64Bytes TargetScenarioId = new FixedString64Bytes("scenario.space4x.module.combat.smoke");

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

            SeedCombatTestCarriers();
            Enabled = false;
        }

        private void SeedCombatTestCarriers()
        {
            // Spawn attacker carrier at origin
            var attacker = CreateCarrierWithModules(
                new float3(0f, 0f, 0f),
                "carrier.attacker",
                new ModuleSpec[] {
                    new ModuleSpec { Class = ModuleClass.BeamCannon, MountType = MountType.MainGun, MountSize = MountSize.Medium },
                    new ModuleSpec { Class = ModuleClass.Engine, MountType = MountType.UtilityBay, MountSize = MountSize.Large }
                }
            );

            // Spawn target carrier at distance
            var target = CreateCarrierWithModules(
                new float3(50f, 0f, 0f),
                "carrier.target",
                new ModuleSpec[] {
                    new ModuleSpec { Class = ModuleClass.BeamCannon, MountType = MountType.MainGun, MountSize = MountSize.Medium },
                    new ModuleSpec { Class = ModuleClass.Engine, MountType = MountType.UtilityBay, MountSize = MountSize.Large }
                }
            );

            // Note: Combat engagement setup is handled by game-specific systems (e.g., Space4XCombatInitiationSystem)
            // Carriers are positioned for combat (50 units apart) and have all required components
        }

        private Entity CreateCarrierWithModules(float3 position, FixedString64Bytes carrierId, ModuleSpec[] modules)
        {
            var carrier = EntityManager.CreateEntity();
            
            // Core transform and carrier components
            EntityManager.SetComponentData(carrier, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            
            EntityManager.AddComponentData(carrier, new CarrierRefitState
            {
                InRefitFacility = 0,
                SpeedMultiplier = 1f
            });

            EntityManager.AddComponent<CarrierModuleAggregate>(carrier);

            var slots = EntityManager.AddBuffer<CarrierModuleSlot>(carrier);
            EntityManager.AddBuffer<ModuleRepairTicket>(carrier);
            EntityManager.AddBuffer<ModuleRefitRequest>(carrier);

            // Add combat-required components
            EntityManager.AddBuffer<CombatHitEvent>(carrier);
            EntityManager.AddComponentData(carrier, new VerticalEngagementRange
            {
                VerticalRange = 100f,
                HorizontalRange = 100f
            });
            EntityManager.AddComponentData(carrier, new Advantage3D
            {
                HighGroundBonus = 0f,
                FlankingBonus = 0f,
                VerticalAdvantage = 0f
            });

            // Create modules
            for (byte i = 0; i < modules.Length; i++)
            {
                var spec = modules[i];
                var moduleEntity = CreateModuleEntity(carrier, spec, i);

                slots.Add(new CarrierModuleSlot
                {
                    Type = spec.MountType,
                    Size = spec.MountSize,
                    InstalledModule = moduleEntity,
                    Priority = GetDefaultPriority(spec.Class)
                });
            }

            return carrier;
        }

        private Entity CreateModuleEntity(Entity carrier, ModuleSpec spec, byte slotIndex)
        {
            var module = EntityManager.CreateEntity();
            
            EntityManager.AddComponentData(module, new Parent { Value = carrier });
            
            EntityManager.AddComponentData(module, new ShipModule
            {
                ModuleId = new FixedString64Bytes($"{spec.Class}_{slotIndex}"),
                Family = GetFamilyFromClass(spec.Class),
                Class = spec.Class,
                RequiredMount = spec.MountType,
                RequiredSize = spec.MountSize,
                Mass = 10f,
                PowerRequired = spec.Class == ModuleClass.Engine ? 0f : 5f,
                OffenseRating = spec.Class == ModuleClass.BeamCannon ? 50f : 0f,
                DefenseRating = 0f,
                UtilityRating = spec.Class == ModuleClass.Engine ? 100f : 0f,
                EfficiencyPercent = 100,
                State = ModuleState.Active
            });

            // Use PureDOTS.Runtime.Ships.ModuleHealth (float-based) for combat compatibility
            EntityManager.AddComponentData(module, new ModuleHealth
            {
                MaxHealth = 100f,
                Health = 100f,
                DegradationPerTick = 0f,
                FailureThreshold = 25f,
                State = ModuleHealthState.Nominal,
                Flags = ModuleHealthFlags.None,
                LastProcessedTick = 0
            });

            // Add combat-required components
            EntityManager.AddComponentData(module, new ModulePosition
            {
                LocalPosition = new float3(0f, slotIndex * 2f, 0f), // Stub: vertical stacking
                Radius = 1.5f
            });

            EntityManager.AddComponentData(module, new ModuleTargetPriority
            {
                Priority = GetDefaultPriority(spec.Class)
            });

            EntityManager.AddComponentData(module, new CarrierOwner { Carrier = carrier });
            EntityManager.AddComponentData(module, new ModuleOperationalState
            {
                IsOnline = 1,
                InCombat = 0,
                LoadFactor = 0f
            });

            return module;
        }

        private ModuleFamily GetFamilyFromClass(ModuleClass moduleClass)
        {
            return moduleClass switch
            {
                ModuleClass.BeamCannon or ModuleClass.MassDriver or ModuleClass.Missile or ModuleClass.PointDefense => ModuleFamily.Weapon,
                ModuleClass.Shield or ModuleClass.Armor => ModuleFamily.Defense,
                ModuleClass.Engine or ModuleClass.Sensor or ModuleClass.Cargo or ModuleClass.Hangar => ModuleFamily.Utility,
                ModuleClass.Fabrication or ModuleClass.Research or ModuleClass.Medical => ModuleFamily.Facility,
                _ => ModuleFamily.Utility
            };
        }

        private byte GetDefaultPriority(ModuleClass moduleClass)
        {
            return moduleClass switch
            {
                ModuleClass.Engine => 200,
                ModuleClass.BeamCannon => 150,
                ModuleClass.MassDriver => 150,
                ModuleClass.Missile => 150,
                ModuleClass.PointDefense => 140,
                ModuleClass.Shield => 120,
                ModuleClass.Armor => 100,
                ModuleClass.Sensor => 80,
                ModuleClass.Cargo => 50,
                ModuleClass.Hangar => 60,
                ModuleClass.Fabrication => 40,
                ModuleClass.Research => 40,
                ModuleClass.Medical => 30,
                _ => 20
            };
        }

        private struct ModuleSpec
        {
            public ModuleClass Class;
            public MountType MountType;
            public MountSize MountSize;
        }
    }
}

