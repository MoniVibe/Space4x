#if UNITY_EDITOR
using System;
using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Authoring.Space
{
    [DisallowMultipleComponent]
    public sealed class CarrierModuleLoadoutAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct ModuleDefinition
        {
            public ModuleFamily family;
            public ModuleClass moduleClass;
            public string moduleName;
            public float mass;
            public float powerRequired;
            public float powerGeneration;
            [Range(0, 100)] public byte efficiencyPercent;
            public ModuleState state;
            [Range(0, 100)] public byte failureThreshold;
            [Range(0, 10)] public byte repairPriority;
            public float passiveDegradationPerSecond;
            public float activeDegradationPerSecond;
            public float cargoCapacity;
            public float miningRate;
            public float repairRateBonus;
        }

        [Serializable]
        public struct SlotDefinition
        {
            public MountType mountType;
            public MountSize mountSize;
            public ModuleDefinition module;
        }

        [Header("Refit / Repair")]
        public float fieldRefitRate = 3f;
        public float stationRefitRate = 8f;
        public bool atRefitFacilityOnStart = true;
        [Min(0f)] public float maxPowerOutput = 12f;

        [Header("Slots")]
        public SlotDefinition[] slots = Array.Empty<SlotDefinition>();

        [Header("Telemetry")]
        [Tooltip("Log when power budget is exceeded on bake to aid validation.")]
        public bool logPowerBudget = true;
    }

    public sealed class CarrierModuleLoadoutBaker : Baker<CarrierModuleLoadoutAuthoring>
    {
        public override void Bake(CarrierModuleLoadoutAuthoring authoring)
        {
            var carrier = GetEntity(authoring, TransformUsageFlags.Dynamic);

            AddComponent(carrier, new CarrierRefitState
            {
                FieldRefitRate = math.max(0f, authoring.fieldRefitRate),
                StationRefitRate = math.max(0f, authoring.stationRefitRate),
                AtRefitFacility = authoring.atRefitFacilityOnStart
            });

            AddComponent<CarrierModuleStatTotals>(carrier);
            AddComponent(carrier, new CarrierPowerBudget
            {
                MaxPowerOutput = math.max(0f, authoring.maxPowerOutput),
                CurrentDraw = 0f,
                CurrentGeneration = 0f,
                OverBudget = false
            });
            var slotsBuffer = AddBuffer<CarrierModuleSlot>(carrier);
            AddBuffer<ModuleRepairTicket>(carrier);
            AddBuffer<CarrierModuleRefitRequest>(carrier);

            if (authoring.slots == null || authoring.slots.Length == 0)
            {
                return;
            }

            for (byte i = 0; i < authoring.slots.Length; i++)
            {
                var slotDef = authoring.slots[i];
                var moduleEntity = CreateModuleEntity(carrier, slotDef.module, slotDef.mountType, slotDef.mountSize, i);

                slotsBuffer.Add(new CarrierModuleSlot
                {
                    SlotIndex = i,
                    Type = slotDef.mountType,
                    Size = slotDef.mountSize,
                    InstalledModule = moduleEntity
                });
            }

            if (authoring.logPowerBudget && TryCalculatePower(authoring.slots, out var draw, out var generation) && draw > authoring.maxPowerOutput)
            {
                UnityEngine.Debug.LogWarning($"CarrierModuleLoadoutAuthoring: Power draw {draw:F2} exceeds max output {authoring.maxPowerOutput:F2} on '{authoring.name}'. Refit/repair will respect over-budget gating at runtime.", authoring);
            }
        }

        private Entity CreateModuleEntity(Entity carrier, CarrierModuleLoadoutAuthoring.ModuleDefinition def, MountType mountType, MountSize mountSize, byte slotIndex)
        {
            var module = CreateAdditionalEntity(TransformUsageFlags.Dynamic);

            AddComponent(module, new Parent { Value = carrier });
            AddComponent(module, new ShipModule
            {
                Family = def.family,
                Class = def.moduleClass,
                RequiredMount = mountType,
                RequiredSize = mountSize,
                ModuleName = new FixedString64Bytes(string.IsNullOrWhiteSpace(def.moduleName) ? def.moduleClass.ToString() : def.moduleName.Trim()),
                Mass = math.max(0f, def.mass),
                PowerRequired = math.max(0f, def.powerRequired),
                PowerGeneration = math.max(0f, def.powerGeneration),
                EfficiencyPercent = (byte)math.clamp((int)def.efficiencyPercent, 0, 100),
                State = def.state
            });

            AddComponent(module, new ModuleStatModifier
            {
                Mass = math.max(0f, def.mass),
                PowerDraw = math.max(0f, def.powerRequired),
                PowerGeneration = math.max(0f, def.powerGeneration),
                CargoCapacity = math.max(0f, def.cargoCapacity),
                MiningRate = math.max(0f, def.miningRate),
                RepairRateBonus = math.max(0f, def.repairRateBonus)
            });

            // Use PureDOTS.Runtime.Ships.ModuleHealth (float-based) for combat systems compatibility
            // PureDOTS.Runtime.Space.ModuleHealth (byte Integrity) is incompatible with combat systems
            float maxHealth = 100f;
            float failureThreshold = math.clamp((float)def.failureThreshold, 0f, 100f);
            
            AddComponent(module, new PureDOTS.Runtime.Ships.ModuleHealth
            {
                MaxHealth = maxHealth,
                Health = maxHealth,
                DegradationPerTick = 0f,
                FailureThreshold = failureThreshold,
                State = PureDOTS.Runtime.Ships.ModuleHealthState.Nominal,
                Flags = PureDOTS.Runtime.Ships.ModuleHealthFlags.None,
                LastProcessedTick = 0
            });

            AddComponent(module, new ModuleDegradation
            {
                PassivePerSecond = math.max(0f, def.passiveDegradationPerSecond),
                ActivePerSecond = math.max(0f, def.activeDegradationPerSecond),
                CombatMultiplier = 1f
            });

            // Add ModulePosition for hit detection (stub with slot-based position)
            AddComponent(module, new ModulePosition
            {
                LocalPosition = new float3(0f, slotIndex * 2f, 0f), // Stub: vertical stacking based on slot index
                Radius = 1.5f
            });

            // Add ModuleTargetPriority based on module class
            AddComponent(module, new ModuleTargetPriority
            {
                Priority = GetDefaultPriority(def.moduleClass)
            });

            return module;
        }

        /// <summary>
        /// Gets default priority for a module class (matches ModuleTargetingService.GetDefaultPriority).
        /// </summary>
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

        private bool TryCalculatePower(CarrierModuleLoadoutAuthoring.SlotDefinition[] slots, out float draw, out float generation)
        {
            draw = 0f;
            generation = 0f;

            foreach (var slot in slots)
            {
                var module = slot.module;
                draw += math.max(0f, module.powerRequired);
                generation += math.max(0f, module.powerGeneration);
            }

            return true;
        }
    }
}
#endif
