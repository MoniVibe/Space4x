using PureDOTS.Runtime.Items;
using PureDOTS.Runtime.Shared;
using PureDOTS.Systems.Items;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Authoring.Items
{
    /// <summary>
    /// Authoring ScriptableObject for composite item part catalog.
    /// Defines part types and material properties used by composite items.
    /// </summary>
    public class ItemPartCatalogAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public class PartSpecAuthoring
        {
            [Tooltip("Part type ID (must be unique)")]
            public ushort partTypeId;

            [Tooltip("Part name for display")]
            public string partName;

            [Tooltip("Aggregation weight (0-1) - contribution to parent item")]
            [Range(0f, 1f)]
            public float aggregationWeight = 0.1f;

            [Tooltip("Durability multiplier applied to base material")]
            [Range(0.1f, 2f)]
            public float durabilityMultiplier = 1f;

            [Tooltip("Minimum repair skill level (0-100) required")]
            [Range(0, 100)]
            public byte repairSkillRequired = 25;

            [Tooltip("Is this part critical? (Item breaks if this breaks)")]
            public bool isCritical = false;

            [Tooltip("Durability threshold (0-1) below which part is 'damaged'")]
            [Range(0f, 1f)]
            public float damageThreshold01 = 0.3f;
        }

        [System.Serializable]
        public class MaterialAuthoring
        {
            [Tooltip("Material name")]
            public string materialName;

            [Tooltip("Durability modifier multiplier (e.g., Mithril = 1.5x, Iron = 1.0x, Wood = 0.7x)")]
            [Range(0.1f, 3f)]
            public float durabilityMod = 1f;
        }

        [Header("Part Specifications")]
        public List<PartSpecAuthoring> partSpecs = new List<PartSpecAuthoring>();

        [Header("Materials")]
        public List<MaterialAuthoring> materials = new List<MaterialAuthoring>();
    }

    /// <summary>
    /// Baker for ItemPartCatalogAuthoring.
    /// Creates singleton ItemPartCatalogBlobRef with baked blob asset.
    /// </summary>
    public sealed class ItemPartCatalogBaker : Baker<ItemPartCatalogAuthoring>
    {
        public override void Bake(ItemPartCatalogAuthoring authoring)
        {
            using var bb = new BlobBuilder(Allocator.Temp);
            ref var root = ref bb.ConstructRoot<ItemPartCatalogBlob>();

            // Bake part specs
            var partSpecs = bb.Allocate(ref root.PartSpecs, authoring.partSpecs.Count);
            for (int i = 0; i < authoring.partSpecs.Count; i++)
            {
                var src = authoring.partSpecs[i];
                partSpecs[i] = new ItemPartSpec
                {
                    PartTypeId = src.partTypeId,
                    PartName = new FixedString64Bytes(src.partName),
                    AggregationWeight = src.aggregationWeight,
                    DurabilityMultiplier = src.durabilityMultiplier,
                    RepairSkillRequired = src.repairSkillRequired,
                    IsCritical = src.isCritical,
                    DamageThreshold01 = (half)src.damageThreshold01
                };
            }

            // Bake materials
            var materialNames = bb.Allocate(ref root.MaterialNames, authoring.materials.Count);
            var materialMods = bb.Allocate(ref root.MaterialDurabilityMods, authoring.materials.Count);
            for (int i = 0; i < authoring.materials.Count; i++)
            {
                var src = authoring.materials[i];
                materialNames[i] = new FixedString32Bytes(src.materialName);
                materialMods[i] = src.durabilityMod;
            }

            var blob = bb.CreateBlobAssetReference<ItemPartCatalogBlob>(Allocator.Persistent);
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ItemPartCatalogBlobRef { Blob = blob });
        }
    }

    /// <summary>
    /// Authoring component for composite items (attached to prefabs).
    /// Defines the parts that make up this composite item.
    /// </summary>
    public class CompositeItemAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public class PartAuthoring
        {
            [Tooltip("Part type ID from catalog")]
            public ushort partTypeId;

            [Tooltip("Material name")]
            public string material = "Iron";

            [Tooltip("Quality (0-1)")]
            [Range(0f, 1f)]
            public float quality01 = 0.5f;

            [Tooltip("Initial durability (0-1)")]
            [Range(0f, 1f)]
            public float durability01 = 1f;

            [Tooltip("Rarity weight (0-255)")]
            [Range(0, 255)]
            public byte rarityWeight = 50;
        }

        [Header("Parts")]
        public List<PartAuthoring> parts = new List<PartAuthoring>();

        [Header("Owner")]
        [Tooltip("Parent entity (optional - leave null if this is the root)")]
        public GameObject ownerEntity;
    }

    /// <summary>
    /// Baker for CompositeItemAuthoring.
    /// Creates CompositeItem component and ItemPart buffer.
    /// </summary>
    public sealed class CompositeItemBaker : Baker<CompositeItemAuthoring>
    {
        public override void Bake(CompositeItemAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add CompositeItem component
            Entity ownerEntity = Entity.Null;
            if (authoring.ownerEntity != null)
            {
                ownerEntity = GetEntity(authoring.ownerEntity, TransformUsageFlags.None);
            }

            AddComponent(entity, new CompositeItem
            {
                OwnerEntity = ownerEntity,
                AggregatedDurability = (half)0f,
                AggregatedTier = QualityTier.Poor,
                AggregationHash = 0,
                Flags = CompositeItemFlags.None
            });

            // Add ItemPart buffer
            var partsBuffer = AddBuffer<ItemPart>(entity);
            foreach (var partAuth in authoring.parts)
            {
                partsBuffer.Add(new ItemPart
                {
                    PartTypeId = partAuth.partTypeId,
                    Material = new FixedString32Bytes(partAuth.material),
                    Quality01 = (half)partAuth.quality01,
                    Durability01 = (half)partAuth.durability01,
                    RarityWeight = partAuth.rarityWeight,
                    Flags = PartFlags.None
                });
            }

            // Add DurabilityWearEvent buffer (for runtime wear events)
            AddBuffer<DurabilityWearEvent>(entity);
        }
    }
}

