using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Ships;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Service for routing damage to modules.
    /// </summary>
    [BurstCompile]
    public static class ModuleDamageRouterService
    {
        /// <summary>
        /// Routes damage to a specific module.
        /// </summary>
        public static void RouteDamageToModule(
            EntityStorageInfoLookup entityLookup,
            ComponentLookup<ModuleHealth> healthLookup,
            ComponentLookup<ShipModule> moduleLookup,
            Entity ship,
            Entity module,
            float damageAmount)
        {
            if (!entityLookup.Exists(module))
            {
                return;
            }

            // Ensure module has health component
            if (!healthLookup.HasComponent(module))
            {
                return;
            }

            var health = healthLookup[module];

            // Apply damage
            float newHealth = math.max(0f, health.Health - damageAmount);
            health.Health = newHealth;
            healthLookup[module] = health;

            // Update module state based on health
            UpdateModuleStateFromHealth(ref healthLookup, module, newHealth, health.MaxHealth, health.FailureThreshold);

            // Update ShipModule state if present
            if (moduleLookup.HasComponent(module))
            {
                var shipModule = moduleLookup[module];

                if (newHealth <= 0f)
                {
                    shipModule.State = ModuleState.Destroyed;
                }
                else if (newHealth < health.FailureThreshold)
                {
                    shipModule.State = ModuleState.Damaged;
                }
                else if (shipModule.State == ModuleState.Destroyed)
                {
                    // Module was repaired
                    shipModule.State = ModuleState.Standby;
                }

                moduleLookup[module] = shipModule;
            }
        }

        /// <summary>
        /// Finds module at a given position on a ship.
        /// Uses ModuleTargetingService.DetectModuleHit internally.
        /// </summary>
        public static Entity FindModuleAtPosition(
            EntityStorageInfoLookup entityLookup,
            BufferLookup<CarrierModuleSlot> slotLookup,
            ComponentLookup<Unity.Transforms.LocalTransform> transformLookup,
            ComponentLookup<ModulePosition> positionLookup,
            Entity ship,
            float3 position)
        {
            return ModuleTargetingService.DetectModuleHit(entityLookup, slotLookup, transformLookup, positionLookup, ship, position);
        }

        /// <summary>
        /// Gets current health of a module.
        /// </summary>
        public static float GetModuleHealth(
            EntityStorageInfoLookup entityLookup,
            ComponentLookup<ModuleHealth> healthLookup,
            Entity module)
        {
            if (!entityLookup.Exists(module))
            {
                return 0f;
            }

            if (!healthLookup.HasComponent(module))
            {
                return 0f;
            }

            var health = healthLookup[module];
            return health.Health;
        }

        /// <summary>
        /// Updates module health state based on current health value.
        /// </summary>
        [BurstCompile]
        private static void UpdateModuleStateFromHealth(
            ref ComponentLookup<ModuleHealth> healthLookup,
            in Entity module,
            float currentHealth,
            float maxHealth,
            float failureThreshold)
        {
            if (!healthLookup.HasComponent(module))
            {
                return;
            }

            var health = healthLookup[module];

            // Update health state
            if (currentHealth <= 0f)
            {
                health.State = ModuleHealthState.Destroyed;
                health.Flags |= ModuleHealthFlags.PendingRepairQueue;
            }
            else if (currentHealth < failureThreshold)
            {
                health.State = ModuleHealthState.Failed;
                health.Flags |= ModuleHealthFlags.PendingRepairQueue;
            }
            else if (currentHealth < maxHealth * 0.5f)
            {
                health.State = ModuleHealthState.Degraded;
            }
            else
            {
                health.State = ModuleHealthState.Nominal;
            }

            healthLookup[module] = health;
        }
    }
}

