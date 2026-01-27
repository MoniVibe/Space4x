using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Ships;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Service for managing ship capabilities based on module states.
    /// </summary>
    [BurstCompile]
    public static class CapabilityDisableService
    {
        /// <summary>
        /// Disables a capability on an entity.
        /// </summary>
        public static void DisableCapability(
            EntityStorageInfoLookup entityLookup,
            ComponentLookup<CapabilityState> capabilityStateLookup,
            EntityCommandBuffer ecb,
            Entity entity,
            CapabilityType capability)
        {
            if (!entityLookup.Exists(entity))
            {
                return;
            }

            // Ensure CapabilityState component exists
            if (!capabilityStateLookup.HasComponent(entity))
            {
                ecb.SetComponent(entity, new CapabilityState
                {
                    EnabledCapabilities = CapabilityFlags.None
                });
                return;
            }

            var capabilityState = capabilityStateLookup[entity];

            // Remove capability flag
            CapabilityFlags flag = GetCapabilityFlag(capability);
            capabilityState.EnabledCapabilities &= ~flag;

            capabilityStateLookup[entity] = capabilityState;
        }

        /// <summary>
        /// Enables a capability on an entity.
        /// </summary>
        public static void EnableCapability(
            EntityStorageInfoLookup entityLookup,
            ComponentLookup<CapabilityState> capabilityStateLookup,
            EntityCommandBuffer ecb,
            Entity entity,
            CapabilityType capability)
        {
            if (!entityLookup.Exists(entity))
            {
                return;
            }

            // Ensure CapabilityState component exists
            if (!capabilityStateLookup.HasComponent(entity))
            {
                ecb.SetComponent(entity, new CapabilityState
                {
                    EnabledCapabilities = CapabilityFlags.None
                });
                return;
            }

            var capabilityState = capabilityStateLookup[entity];

            // Add capability flag
            CapabilityFlags flag = GetCapabilityFlag(capability);
            capabilityState.EnabledCapabilities |= flag;

            capabilityStateLookup[entity] = capabilityState;
        }

        /// <summary>
        /// Checks if a capability is enabled.
        /// </summary>
        public static bool IsCapabilityEnabled(
            EntityStorageInfoLookup entityLookup,
            ComponentLookup<CapabilityState> capabilityStateLookup,
            Entity entity,
            CapabilityType capability)
        {
            if (!entityLookup.Exists(entity))
            {
                return false;
            }

            if (!capabilityStateLookup.HasComponent(entity))
            {
                return false;
            }

            var capabilityState = capabilityStateLookup[entity];
            CapabilityFlags flag = GetCapabilityFlag(capability);

            return (capabilityState.EnabledCapabilities & flag) != 0;
        }

        /// <summary>
        /// Updates capabilities based on module states on a ship.
        /// </summary>
        public static void UpdateCapabilitiesFromModules(
            EntityStorageInfoLookup entityLookup,
            BufferLookup<CarrierModuleSlot> slotLookup,
            ComponentLookup<ShipModule> moduleLookup,
            ComponentLookup<ModuleHealth> healthLookup,
            ComponentLookup<CapabilityState> capabilityStateLookup,
            ComponentLookup<CapabilityEffectiveness> effectivenessLookup,
            EntityCommandBuffer ecb,
            Entity ship)
        {
            if (!entityLookup.Exists(ship))
            {
                return;
            }

            // Get ship's module slots
            if (!slotLookup.HasBuffer(ship))
            {
                return;
            }

            var slots = slotLookup[ship];

            // Track which capabilities have at least one functional module
            CapabilityFlags enabledCapabilities = CapabilityFlags.None;
            var effectiveness = new CapabilityEffectiveness
            {
                MovementEffectiveness = 0f,
                FiringEffectiveness = 0f,
                ShieldEffectiveness = 0f,
                SensorEffectiveness = 0f,
                CommunicationEffectiveness = 0f,
                LifeSupportEffectiveness = 0f
            };

            int movementModuleCount = 0;
            int firingModuleCount = 0;
            int shieldModuleCount = 0;
            int sensorModuleCount = 0;
            int communicationModuleCount = 0;
            int lifeSupportModuleCount = 0;

            // Scan all modules
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.InstalledModule == Entity.Null)
                {
                    continue;
                }

                var moduleEntity = slot.InstalledModule;
                if (!entityLookup.Exists(moduleEntity))
                {
                    continue;
                }

                if (!moduleLookup.HasComponent(moduleEntity))
                {
                    continue;
                }

                var module = moduleLookup[moduleEntity];
                var moduleClass = module.Class;
                var moduleState = module.State;

                // Skip destroyed modules
                if (moduleState == ModuleState.Destroyed)
                {
                    continue;
                }

                // Get module health for effectiveness calculation
                float moduleEffectiveness = 1f;
                if (healthLookup.HasComponent(moduleEntity))
                {
                    var health = healthLookup[moduleEntity];
                    if (health.MaxHealth > 0f)
                    {
                        moduleEffectiveness = math.clamp(health.Health / health.MaxHealth, 0f, 1f);
                    }

                    // Destroyed or failed modules contribute nothing
                    if (health.State == ModuleHealthState.Destroyed || health.State == ModuleHealthState.Failed)
                    {
                        continue;
                    }
                }

                // Map module class to capability
                switch (moduleClass)
                {
                    case ModuleClass.Engine:
                        enabledCapabilities |= CapabilityFlags.Movement;
                        movementModuleCount++;
                        effectiveness.MovementEffectiveness += moduleEffectiveness;
                        break;

                    case ModuleClass.BeamCannon:
                    case ModuleClass.MassDriver:
                    case ModuleClass.Missile:
                    case ModuleClass.PointDefense:
                        enabledCapabilities |= CapabilityFlags.Firing;
                        firingModuleCount++;
                        effectiveness.FiringEffectiveness += moduleEffectiveness;
                        break;

                    case ModuleClass.Shield:
                        enabledCapabilities |= CapabilityFlags.Shields;
                        shieldModuleCount++;
                        effectiveness.ShieldEffectiveness += moduleEffectiveness;
                        break;

                    case ModuleClass.Sensor:
                        enabledCapabilities |= CapabilityFlags.Sensors;
                        sensorModuleCount++;
                        effectiveness.SensorEffectiveness += moduleEffectiveness;
                        break;

                    // Communications and LifeSupport not directly mapped from ModuleClass
                    // These would need additional components or different mapping
                }
            }

            // Average effectiveness across modules
            if (movementModuleCount > 0)
            {
                effectiveness.MovementEffectiveness /= movementModuleCount;
            }
            if (firingModuleCount > 0)
            {
                effectiveness.FiringEffectiveness /= firingModuleCount;
            }
            if (shieldModuleCount > 0)
            {
                effectiveness.ShieldEffectiveness /= shieldModuleCount;
            }
            if (sensorModuleCount > 0)
            {
                effectiveness.SensorEffectiveness /= sensorModuleCount;
            }

            // Update capability state
            if (!capabilityStateLookup.HasComponent(ship))
            {
                ecb.SetComponent(ship, new CapabilityState
                {
                    EnabledCapabilities = enabledCapabilities
                });
            }
            else
            {
                var capabilityState = capabilityStateLookup[ship];
                capabilityState.EnabledCapabilities = enabledCapabilities;
                capabilityStateLookup[ship] = capabilityState;
            }

            // Update effectiveness
            if (!effectivenessLookup.HasComponent(ship))
            {
                ecb.AddComponent(ship, effectiveness);
            }
            else
            {
                effectivenessLookup[ship] = effectiveness;
            }
        }

        /// <summary>
        /// Gets capability flag for a capability type.
        /// </summary>
        [BurstCompile]
        private static CapabilityFlags GetCapabilityFlag(CapabilityType capability)
        {
            return capability switch
            {
                CapabilityType.Movement => CapabilityFlags.Movement,
                CapabilityType.Firing => CapabilityFlags.Firing,
                CapabilityType.Shields => CapabilityFlags.Shields,
                CapabilityType.Sensors => CapabilityFlags.Sensors,
                CapabilityType.Communications => CapabilityFlags.Communications,
                CapabilityType.LifeSupport => CapabilityFlags.LifeSupport,
                _ => CapabilityFlags.None
            };
        }
    }
}

