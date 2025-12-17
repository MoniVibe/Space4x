using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module Catalog")]
    public sealed class ModuleCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class ModuleSpecData
        {
            public string id;
            public ModuleClass moduleClass;
            public MountType requiredMount;
            public MountSize requiredSize;
            public float massTons;
            public float powerDrawMW;
            [Range(0, 10)] public byte offenseRating;
            [Range(0, 10)] public byte defenseRating;
            [Range(0, 10)] public byte utilityRating;
            [Range(0f, 1f)] public float defaultEfficiency = 1f;
            // Prefab generation metadata
            [Header("Prefab Metadata")]
            public ModuleFunction function = ModuleFunction.None;
            [Tooltip("Function-specific capacity (e.g., hangar capacity, cargo capacity)")]
            public float functionCapacity = 0f;
            [Tooltip("Human-readable function description")]
            public string functionDescription = string.Empty;
            [Header("Quality/Rarity/Tier/Manufacturer")]
            [Tooltip("Quality in [0, 1]. Fine control over spread/dispersion, misfire risk, maintenance load")]
            [Range(0f, 1f)]
            public float quality = 1f;
            [Tooltip("Rarity level (affects availability, black-market value, diplomatic leverage)")]
            public ModuleRarity rarity = ModuleRarity.Common;
            [Tooltip("Tier (0-255). Drives baseline performance, reliability")]
            [Range(0, 255)]
            public byte tier = 1;
            [Tooltip("Manufacturer ID (references manufacturer catalog for signature traits)")]
            public string manufacturerId = string.Empty;
            [Header("Facility Archetype/Tier (for facility modules)")]
            [Tooltip("Facility archetype (Refinery, Fabricator, etc.)")]
            public FacilityArchetype facilityArchetype = FacilityArchetype.None;
            [Tooltip("Facility tier (Small, Medium, Large, Massive, Titanic)")]
            public FacilityTier facilityTier = FacilityTier.Small;
        }

        public List<ModuleSpecData> modules = new List<ModuleSpecData>();

        public sealed class Baker : Unity.Entities.Baker<ModuleCatalogAuthoring>
        {
            public override void Bake(ModuleCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.modules == null || authoring.modules.Count == 0)
                {
                    UnityDebug.LogWarning("ModuleCatalogAuthoring has no modules defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<ModuleCatalogBlob>();
                var moduleArray = builder.Allocate(ref catalogBlob.Modules, authoring.modules.Count);

                for (int i = 0; i < authoring.modules.Count; i++)
                {
                    var moduleData = authoring.modules[i];
                    moduleArray[i] = new ModuleSpec
                    {
                        Id = new FixedString64Bytes(moduleData.id ?? string.Empty),
                        Class = moduleData.moduleClass,
                        RequiredMount = moduleData.requiredMount,
                        RequiredSize = moduleData.requiredSize,
                        MassTons = math.max(0f, moduleData.massTons),
                        PowerDrawMW = moduleData.powerDrawMW,
                        OffenseRating = (byte)math.clamp(moduleData.offenseRating, 0, 10),
                        DefenseRating = (byte)math.clamp(moduleData.defenseRating, 0, 10),
                        UtilityRating = (byte)math.clamp(moduleData.utilityRating, 0, 10),
                        DefaultEfficiency = math.clamp(moduleData.defaultEfficiency, 0f, 1f),
                        Function = moduleData.function,
                        FunctionCapacity = math.max(0f, moduleData.functionCapacity),
                        FunctionDescription = new FixedString64Bytes(moduleData.functionDescription ?? string.Empty),
                        Quality = math.clamp(moduleData.quality, 0f, 1f),
                        Rarity = moduleData.rarity,
                        Tier = moduleData.tier,
                        ManufacturerId = new FixedString64Bytes(moduleData.manufacturerId ?? string.Empty),
                        FacilityArchetype = moduleData.facilityArchetype,
                        FacilityTier = moduleData.facilityTier
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<ModuleCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ModuleCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

