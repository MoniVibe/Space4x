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
    [AddComponentMenu("Space4X/Hull Catalog")]
    public sealed class HullCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class HullSlotData
        {
            public MountType type;
            public MountSize size;
        }

        [Serializable]
        public class HullSpecData
        {
            public string id;
            public float baseMassTons;
            public bool fieldRefitAllowed;
            public List<HullSlotData> slots = new List<HullSlotData>();
            // Prefab generation metadata
            [Header("Prefab Metadata")]
            public HullCategory category = HullCategory.Other;
            [Tooltip("Total hangar capacity (sum of all hangar modules)")]
            public float hangarCapacity = 0f;
            [Tooltip("Presentation archetype (e.g., 'capital-ship', 'carrier', 'station')")]
            public string presentationArchetype = string.Empty;
            [Header("Default Style Tokens")]
            [Range(0, 255)] public byte defaultPalette = 0;
            [Range(0, 255)] public byte defaultRoughness = 128;
            [Range(0, 255)] public byte defaultPattern = 0;
            [Header("Variant Metadata")]
            [Tooltip("Hull variant (Common, Uncommon, Heroic, Prototype)")]
            public HullVariant variant = HullVariant.Common;
            [Tooltip("Built-in module loadouts (pre-configured module IDs)")]
            public List<string> builtInModuleLoadouts = new List<string>();
        }

        public List<HullSpecData> hulls = new List<HullSpecData>();

        public sealed class Baker : Unity.Entities.Baker<HullCatalogAuthoring>
        {
            public override void Bake(HullCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.hulls == null || authoring.hulls.Count == 0)
                {
                    UnityDebug.LogWarning("HullCatalogAuthoring has no hulls defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<HullCatalogBlob>();
                var hullArray = builder.Allocate(ref catalogBlob.Hulls, authoring.hulls.Count);

                for (int i = 0; i < authoring.hulls.Count; i++)
                {
                    var hullData = authoring.hulls[i];
                    ref var hullSpec = ref hullArray[i];
                    var slotCount = hullData.slots != null ? hullData.slots.Count : 0;
                    var slotsArray = builder.Allocate(ref hullSpec.Slots, slotCount);

                    for (int j = 0; j < slotCount; j++)
                    {
                        var slotData = hullData.slots[j];
                        slotsArray[j] = new HullSlot
                        {
                            Type = slotData.type,
                            Size = slotData.size
                        };
                    }

                    var loadoutCount = hullData.builtInModuleLoadouts != null ? hullData.builtInModuleLoadouts.Count : 0;
                    var loadoutsArray = builder.Allocate(ref hullSpec.BuiltInModuleLoadouts, loadoutCount);
                    for (int k = 0; k < loadoutCount; k++)
                    {
                        loadoutsArray[k] = new FixedString64Bytes(hullData.builtInModuleLoadouts[k] ?? string.Empty);
                    }

                    builder.Allocate(ref hullSpec.AllowedSegmentFamilies, 0);
                    builder.Allocate(ref hullSpec.RequiredSegmentRoles, 0);
                    builder.Allocate(ref hullSpec.DefaultSegmentIds, 0);

                    hullSpec.Id = new FixedString64Bytes(hullData.id ?? string.Empty);
                    hullSpec.BaseMassTons = math.max(0f, hullData.baseMassTons);
                    hullSpec.FieldRefitAllowed = hullData.fieldRefitAllowed;
                    hullSpec.Class = ResolveClass(hullData.category);
                    hullSpec.MinSegmentCount = 0;
                    hullSpec.MaxSegmentCount = 0;
                    hullSpec.Category = hullData.category;
                    hullSpec.HangarCapacity = math.max(0f, hullData.hangarCapacity);
                    hullSpec.PresentationArchetype = new FixedString64Bytes(hullData.presentationArchetype ?? string.Empty);
                    hullSpec.DefaultStyleTokens = new StyleTokens
                    {
                        Palette = hullData.defaultPalette,
                        Roughness = hullData.defaultRoughness,
                        Pattern = hullData.defaultPattern
                    };
                    hullSpec.Variant = hullData.variant;
                }

                var blobAsset = builder.CreateBlobAssetReference<HullCatalogBlob>(Allocator.Persistent);

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new HullCatalogSingleton { Catalog = blobAsset });
            }

            private static HullClass ResolveClass(HullCategory category)
            {
                return category switch
                {
                    HullCategory.Escort => HullClass.Escort,
                    HullCategory.Carrier => HullClass.Carrier,
                    HullCategory.Station => HullClass.Station,
                    HullCategory.Freighter => HullClass.Freighter,
                    HullCategory.CapitalShip => HullClass.Battleship,
                    _ => HullClass.Other
                };
            }
        }
    }
}
