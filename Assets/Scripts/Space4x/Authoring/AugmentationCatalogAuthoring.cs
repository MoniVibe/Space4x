using System;
using System.Collections.Generic;
using Space4X.Registry;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Catalog for augmentations/implants.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Augmentation Catalog")]
    public sealed class AugmentationCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class AugmentationSpecData
        {
            public string id;
            [Tooltip("Augment name")]
            public string displayName = string.Empty;
            [Tooltip("Augment archetype (Combat, Finesse, Will, General)")]
            public AugmentArchetype archetype = AugmentArchetype.General;
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
            [Tooltip("Slot ID (species-specific limb/organ slot)")]
            public string slotId = string.Empty;
            [Header("Stat Modifiers")]
            [Tooltip("Physique modifier")]
            public float physiqueModifier = 0f;
            [Tooltip("Finesse modifier")]
            public float finesseModifier = 0f;
            [Tooltip("Will modifier")]
            public float willModifier = 0f;
            [Tooltip("General modifier")]
            public float generalModifier = 0f;
            [Header("Metadata")]
            [Tooltip("Upkeep cost")]
            public float upkeepCost = 0f;
            [Tooltip("Risk factor (0-1)")]
            [Range(0f, 1f)]
            public float riskFactor = 0f;
            [Tooltip("Legal status (Licensed, Rogue, BlackMarket)")]
            public LegalStatus legalStatus = LegalStatus.Licensed;
        }

        public enum AugmentArchetype : byte
        {
            Combat = 0,
            Finesse = 1,
            Will = 2,
            General = 3
        }

        public enum LegalStatus : byte
        {
            Licensed = 0,
            Rogue = 1,
            BlackMarket = 2
        }

        public List<AugmentationSpecData> augmentations = new List<AugmentationSpecData>();
    }
}

