using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Ships;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Ships
{
    /// <summary>
    /// Authoring helper that instantiates module entities from a catalog and attaches them to a carrier's slots.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarrierModuleLoadoutAuthoring : MonoBehaviour
    {
        [Tooltip("Catalog asset containing module definitions used by this carrier.")]
        public ModuleCatalogAsset catalog;

        [Tooltip("Slots to seed on this carrier. ModuleId must exist in the catalog.")]
        public Slot[] slots;

        [Header("Defaults")]
        public float defaultMaxHealth = 100f;
        public float defaultFailureThreshold = 25f;
        public float defaultDegradationPerTick = 0f;
    }

    [Serializable]
    public struct Slot
    {
        public string moduleId;
        [Range(0, 255)] public byte priority;
        public bool requiresStationRepair;
        public bool markInCombat;
        public float loadFactor;
    }

    public sealed class CarrierModuleLoadoutBaker : Baker<CarrierModuleLoadoutAuthoring>
    {
        public override void Bake(CarrierModuleLoadoutAuthoring authoring)
        {
            if (authoring.catalog == null || authoring.catalog.Entries == null || authoring.catalog.Entries.Count == 0)
            {
                Debug.LogWarning($"[CarrierModuleLoadoutBaker] Missing module catalog on '{authoring.name}'. No modules baked.");
                return;
            }

            var carrier = GetEntity(TransformUsageFlags.Dynamic);
            var catalog = authoring.catalog;
            var lookup = new Dictionary<FixedString64Bytes, ModuleCatalogEntry>(catalog.Entries.Count);

            foreach (var entry in catalog.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.ModuleId))
                    continue;

                var id = new FixedString64Bytes(entry.ModuleId.Trim().ToLowerInvariant());
                if (!lookup.ContainsKey(id))
                {
                    lookup.Add(id, entry);
                }
            }

            var slotsBuffer = AddBuffer<CarrierModuleSlot>(carrier);

            for (int i = 0; i < authoring.slots.Length; i++)
            {
                var slot = authoring.slots[i];
                if (string.IsNullOrWhiteSpace(slot.moduleId))
                {
                    continue;
                }

                var id = new FixedString64Bytes(slot.moduleId.Trim().ToLowerInvariant());
                if (!lookup.TryGetValue(id, out var definition))
                {
                    Debug.LogWarning($"[CarrierModuleLoadoutBaker] Module id '{slot.moduleId}' not found in catalog for '{authoring.name}'.");
                    continue;
                }

                var moduleEntity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);

                AddComponent(moduleEntity, new ShipModule
                {
                    ModuleId = id,
                    Family = definition.Family,
                    Class = definition.Class,
                    RequiredMount = definition.RequiredMount,
                    RequiredSize = definition.RequiredSize,
                    Mass = definition.Mass,
                    PowerRequired = definition.PowerRequired,
                    OffenseRating = definition.OffenseRating,
                    DefenseRating = definition.DefenseRating,
                    UtilityRating = definition.UtilityRating,
                        EfficiencyPercent = (byte)math.max(1, (int)definition.EfficiencyPercent),
                    State = ModuleState.Standby
                });

                AddComponent(moduleEntity, new ModuleHealth
                {
                    MaxHealth = math.max(1f, authoring.defaultMaxHealth),
                    Health = math.max(1f, authoring.defaultMaxHealth),
                    DegradationPerTick = math.max(0f, authoring.defaultDegradationPerTick),
                    FailureThreshold = math.max(0.01f, authoring.defaultFailureThreshold),
                    State = ModuleHealthState.Nominal,
                    Flags = slot.requiresStationRepair ? ModuleHealthFlags.RequiresStation : ModuleHealthFlags.None,
                    LastProcessedTick = 0
                });

                AddComponent(moduleEntity, new ModuleOperationalState
                {
                    IsOnline = 1,
                    InCombat = slot.markInCombat ? (byte)1 : (byte)0,
                    LoadFactor = math.max(0f, slot.loadFactor)
                });

                AddComponent(moduleEntity, new CarrierOwner { Carrier = carrier });

                slotsBuffer.Add(new CarrierModuleSlot
                {
                    Type = definition.RequiredMount,
                    Size = definition.RequiredSize,
                    InstalledModule = moduleEntity,
                    Priority = slot.priority
                });
            }
        }
    }
}
