using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for installed augmentations.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Augmentation Inventory")]
    public sealed class AugmentationInventoryAuthoring : MonoBehaviour
    {
        [Serializable]
        public class InstalledAugment
        {
            [Tooltip("Slot ID where augment is installed")]
            public string slotId = string.Empty;
            [Tooltip("Augment ID (references augmentation catalog)")]
            public string augmentId = string.Empty;
            [Tooltip("Quality in [0, 1]")]
            [Range(0f, 1f)]
            public float quality = 1f;
            [Tooltip("Tier (0-255)")]
            [Range(0, 255)]
            public byte tier = 1;
            [Tooltip("Rarity level")]
            public ModuleRarity rarity = ModuleRarity.Common;
            [Tooltip("Manufacturer ID")]
            public string manufacturerId = string.Empty;
            [Tooltip("Status flags (bitmask)")]
            public uint statusFlags = 0;
        }

        [Tooltip("Installed augmentations")]
        public List<InstalledAugment> installedAugments = new List<InstalledAugment>();

        public sealed class Baker : Unity.Entities.Baker<AugmentationInventoryAuthoring>
        {
            public override void Bake(AugmentationInventoryAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<Registry.InstalledAugmentation>(entity);

                if (authoring.installedAugments != null)
                {
                    foreach (var augment in authoring.installedAugments)
                    {
                        if (!string.IsNullOrWhiteSpace(augment.augmentId))
                        {
                            buffer.Add(new Registry.InstalledAugmentation
                            {
                                SlotId = new FixedString64Bytes(augment.slotId ?? string.Empty),
                                AugmentId = new FixedString64Bytes(augment.augmentId),
                                Quality = augment.quality,
                                Tier = augment.tier,
                                Rarity = augment.rarity,
                                ManufacturerId = new FixedString64Bytes(augment.manufacturerId ?? string.Empty),
                                StatusFlags = augment.statusFlags
                            });
                        }
                    }
                }
            }
        }
    }
}

