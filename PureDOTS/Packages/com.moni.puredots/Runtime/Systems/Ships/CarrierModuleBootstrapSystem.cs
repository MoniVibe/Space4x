using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using CombatHitEvent = PureDOTS.Runtime.Combat.HitEvent;

// NOTE: Module combat systems require PureDOTS.Runtime.Ships.CarrierModuleSlot (with InstalledModule field).
// Space4X.Registry.CarrierModuleSlot (with CurrentModule) is incompatible. Space4X ships must use
// PureDOTS.Runtime.Ships.CarrierModuleSlot for module combat to function.

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Ensures carriers that expose module slots also carry the supporting buffers/settings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // Removed invalid UpdateAfter: CoreSingletonBootstrapSystem executes in TimeSystemGroup; ordering is handled at group composition.
    public partial struct CarrierModuleBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierModuleSlot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithNone<CarrierModuleAggregate>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new CarrierModuleAggregate { EfficiencyScalar = 1f });
            }

            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithNone<ModuleRepairSettings>().WithEntityAccess())
            {
                ecb.AddComponent(entity, ModuleRepairSettings.CreateDefaults());
            }

            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithNone<CarrierRefitSettings>().WithEntityAccess())
            {
                ecb.AddComponent(entity, CarrierRefitSettings.CreateDefaults());
            }

            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithNone<CarrierRefitState>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new CarrierRefitState { InRefitFacility = 0, SpeedMultiplier = 1f });
            }

            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithNone<ModuleRepairTicket>().WithEntityAccess())
            {
                ecb.AddBuffer<ModuleRepairTicket>(entity);
            }

            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithNone<ModuleRefitRequest>().WithEntityAccess())
            {
                ecb.AddBuffer<ModuleRefitRequest>(entity);
            }

            // Ensure ships with modules have HitEvent buffer for module damage routing
            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasBuffer<CombatHitEvent>(entity))
                {
                    ecb.AddBuffer<CombatHitEvent>(entity);
                }
            }

            // Add 3D formation components to ships with modules
            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithNone<VerticalEngagementRange>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new VerticalEngagementRange
                {
                    VerticalRange = 100f, // Default vertical engagement range
                    HorizontalRange = 100f
                });
            }

            // Add Advantage3D component for combat entities
            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithNone<Advantage3D>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new Advantage3D
                {
                    HighGroundBonus = 0f,
                    FlankingBonus = 0f,
                    VerticalAdvantage = 0f
                });
            }

            foreach (var (slots, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithEntityAccess())
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var module = slots[i].InstalledModule;
                    if (module == Entity.Null || !state.EntityManager.HasComponent<ShipModule>(module))
                    {
                        continue;
                    }

                    if (!state.EntityManager.HasComponent<CarrierOwner>(module))
                    {
                        ecb.AddComponent(module, new CarrierOwner { Carrier = entity });
                    }

                    if (!state.EntityManager.HasComponent<ModuleHealth>(module))
                    {
                        ecb.AddComponent(module, new ModuleHealth
                        {
                            MaxHealth = 100f,
                            Health = 100f,
                            DegradationPerTick = 0f,
                            FailureThreshold = 25f,
                            State = ModuleHealthState.Nominal,
                            Flags = ModuleHealthFlags.None,
                            LastProcessedTick = 0
                        });
                    }

                    if (!state.EntityManager.HasComponent<ModuleOperationalState>(module))
                    {
                        ecb.AddComponent(module, new ModuleOperationalState
                        {
                            IsOnline = 1,
                            InCombat = 0,
                            LoadFactor = 0f
                        });
                    }

                    // Ensure combat-required components exist (may be missing if module created outside baker)
                    if (!state.EntityManager.HasComponent<ModulePosition>(module))
                    {
                        // Use slot index for stub position (vertical stacking)
                        ecb.AddComponent(module, new ModulePosition
                        {
                            LocalPosition = new Unity.Mathematics.float3(0f, i * 2f, 0f),
                            Radius = 1.5f
                        });
                    }

                    if (!state.EntityManager.HasComponent<ModuleTargetPriority>(module))
                    {
                        // Get default priority based on module class
                        if (state.EntityManager.HasComponent<ShipModule>(module))
                        {
                            var shipModule = state.EntityManager.GetComponentData<ShipModule>(module);
                            byte priority = GetDefaultPriority(shipModule.Class);
                            ecb.AddComponent(module, new ModuleTargetPriority { Priority = priority });
                        }
                        else
                        {
                            ecb.AddComponent(module, new ModuleTargetPriority { Priority = 50 }); // Default
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Gets default priority for a module class (matches ModuleTargetingService.GetDefaultPriority).
        /// </summary>
        [BurstCompile]
        private static byte GetDefaultPriority(ModuleClass moduleClass)
        {
            return moduleClass switch
            {
                // Critical systems - highest priority
                ModuleClass.Engine => 200,
                // Weapons - high priority
                ModuleClass.BeamCannon => 150,
                ModuleClass.MassDriver => 150,
                ModuleClass.Missile => 150,
                ModuleClass.PointDefense => 140,
                // Defense - medium-high priority
                ModuleClass.Shield => 120,
                ModuleClass.Armor => 100,
                // Utility - medium priority
                ModuleClass.Sensor => 80,
                ModuleClass.Cargo => 50,
                ModuleClass.Hangar => 60,
                // Facilities - lower priority
                ModuleClass.Fabrication => 40,
                ModuleClass.Research => 40,
                ModuleClass.Medical => 30,
                // Colony - lowest priority
                _ => 20
            };
        }
    }
}
