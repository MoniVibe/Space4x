using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Ships;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Service for module targeting logic.
    /// </summary>
    [BurstCompile]
    public static class ModuleTargetingService
    {
        /// <summary>
        /// Selects a module target on the target ship.
        /// Prioritizes critical modules (bridge, reactor, weapons).
        /// </summary>
        public static Entity SelectModuleTarget(
            EntityStorageInfoLookup entityLookup,
            BufferLookup<CarrierModuleSlot> slotLookup,
            ComponentLookup<ShipModule> moduleLookup,
            ComponentLookup<ModuleTargetPriority> priorityLookup,
            ComponentLookup<ModuleHealth> healthLookup,
            Entity attacker,
            Entity targetShip)
        {
            if (!entityLookup.Exists(targetShip))
            {
                return Entity.Null;
            }

            // Get ship's module slots
            if (!slotLookup.HasBuffer(targetShip))
            {
                return Entity.Null;
            }

            var slots = slotLookup[targetShip];
            if (slots.Length == 0)
            {
                return Entity.Null;
            }

            Entity bestModule = Entity.Null;
            byte bestPriority = 0;

            // Find highest priority targetable module
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

                if (!IsModuleTargetable(entityLookup, moduleLookup, healthLookup, moduleEntity))
                {
                    continue;
                }

                // Get priority (default to 50 if not set)
                byte priority = 50;
                if (priorityLookup.HasComponent(moduleEntity))
                {
                    priority = priorityLookup[moduleEntity].Priority;
                }
                else
                {
                    // Assign default priority based on module class
                    if (moduleLookup.HasComponent(moduleEntity))
                    {
                        var module = moduleLookup[moduleEntity];
                        priority = GetDefaultPriority(module.Class);
                    }
                }

                // Prefer higher priority, or if equal, prefer damaged modules
                if (priority > bestPriority || (priority == bestPriority && bestModule != Entity.Null))
                {
                    bool preferThis = priority > bestPriority;
                    if (!preferThis && priority == bestPriority)
                    {
                        // If same priority, prefer damaged modules
                        if (healthLookup.HasComponent(moduleEntity))
                        {
                            var health = healthLookup[moduleEntity];
                            if (health.State == ModuleHealthState.Degraded || health.State == ModuleHealthState.Failed)
                            {
                                preferThis = true;
                            }
                        }
                    }

                    if (preferThis)
                    {
                        bestModule = moduleEntity;
                        bestPriority = priority;
                    }
                }
            }

            return bestModule;
        }

        /// <summary>
        /// Detects which module was hit based on hit position.
        /// Stubbed initially - uses distance to module positions.
        /// </summary>
        public static Entity DetectModuleHit(
            EntityStorageInfoLookup entityLookup,
            BufferLookup<CarrierModuleSlot> slotLookup,
            ComponentLookup<Unity.Transforms.LocalTransform> transformLookup,
            ComponentLookup<ModulePosition> positionLookup,
            Entity ship,
            float3 hitPosition)
        {
            if (!entityLookup.Exists(ship))
            {
                return Entity.Null;
            }

            // Get ship transform for local space conversion
            if (!transformLookup.HasComponent(ship))
            {
                return Entity.Null;
            }

            var shipTransform = transformLookup[ship];
            var shipWorldPos = shipTransform.Position;
            var shipRotation = shipTransform.Rotation;

            // Convert hit position to local space
            float3 localHitPos = math.mul(math.inverse(shipRotation), hitPosition - shipWorldPos);

            // Get ship's module slots
            if (!slotLookup.HasBuffer(ship))
            {
                return Entity.Null;
            }

            var slots = slotLookup[ship];
            if (slots.Length == 0)
            {
                return Entity.Null;
            }

            Entity closestModule = Entity.Null;
            float closestDistance = float.MaxValue;

            // Find closest module to hit position
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

                if (!positionLookup.HasComponent(moduleEntity))
                {
                    // Stub: if no position data, skip (or use slot index as fallback)
                    continue;
                }

                var modulePos = positionLookup[moduleEntity];
                float3 moduleWorldPos = shipWorldPos + math.mul(shipRotation, modulePos.LocalPosition);
                float distance = math.distance(hitPosition, moduleWorldPos);

                // Check if within module radius
                if (distance <= modulePos.Radius && distance < closestDistance)
                {
                    closestModule = moduleEntity;
                    closestDistance = distance;
                }
            }

            return closestModule;
        }

        /// <summary>
        /// Checks if a module can be targeted.
        /// </summary>
        public static bool IsModuleTargetable(
            EntityStorageInfoLookup entityLookup,
            ComponentLookup<ShipModule> moduleLookup,
            ComponentLookup<ModuleHealth> healthLookup,
            Entity module)
        {
            if (!entityLookup.Exists(module))
            {
                return false;
            }

            // Check if module exists and has ShipModule component
            if (!moduleLookup.HasComponent(module))
            {
                return false;
            }

            var shipModule = moduleLookup[module];

            // Destroyed modules cannot be targeted
            if (shipModule.State == ModuleState.Destroyed)
            {
                return false;
            }

            // Check health state
            if (healthLookup.HasComponent(module))
            {
                var health = healthLookup[module];
                if (health.State == ModuleHealthState.Destroyed)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets default priority for a module class.
        /// Critical modules (reactor, bridge) have higher priority.
        /// </summary>
        [BurstCompile]
        private static byte GetDefaultPriority(ModuleClass moduleClass)
        {
            return moduleClass switch
            {
                // Critical systems - highest priority
                ModuleClass.Engine => 200, // Engines disable movement
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

