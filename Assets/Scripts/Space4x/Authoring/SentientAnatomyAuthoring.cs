using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for sentient anatomy (species-defined limb/organ slots, augmentation compatibility).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Sentient Anatomy")]
    public sealed class SentientAnatomyAuthoring : MonoBehaviour
    {
        [Serializable]
        public class AnatomySlot
        {
            [Tooltip("Slot ID")]
            public string slotId = string.Empty;
            [Tooltip("Slot type (Limb, Organ, Neural, etc.)")]
            public string slotType = string.Empty;
            [Tooltip("Health multiplier")]
            [Range(0f, 2f)]
            public float healthMultiplier = 1f;
            [Tooltip("Compatible augment families (comma-separated)")]
            public string compatibleAugmentFamilies = string.Empty;
        }

        [Tooltip("Species ID")]
        public string speciesId = string.Empty;

        [Tooltip("Anatomy slots (limb/organ layout)")]
        public List<AnatomySlot> slots = new List<AnatomySlot>();

        public sealed class Baker : Unity.Entities.Baker<SentientAnatomyAuthoring>
        {
            public override void Bake(SentientAnatomyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.SentientAnatomy
                {
                    SpeciesId = new FixedString64Bytes(authoring.speciesId ?? string.Empty)
                });

                // Note: Slot data would be stored in a blob asset or buffer
                // For now, we'll store it in a component that can be expanded later
            }
        }
    }
}

