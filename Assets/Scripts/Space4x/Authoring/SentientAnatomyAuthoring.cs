using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Individual;
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

                AddComponent(entity, new PureDOTS.Runtime.Individual.SentientAnatomy
                {
                    SpeciesId = new FixedString64Bytes(authoring.speciesId ?? string.Empty)
                });

                var slotBuffer = AddBuffer<AnatomySlot>(entity);
                if (authoring.slots != null)
                {
                    foreach (var slot in authoring.slots)
                    {
                        if (string.IsNullOrWhiteSpace(slot.slotId))
                        {
                            continue;
                        }

                        slotBuffer.Add(new AnatomySlot
                        {
                            SlotId = new FixedString64Bytes(slot.slotId ?? string.Empty),
                            SlotType = ParseSlotType(slot.slotType),
                            HealthMultiplier = Mathf.Max(0.1f, slot.healthMultiplier),
                            CompatibleFamilies = new FixedString64Bytes(slot.compatibleAugmentFamilies ?? string.Empty)
                        });
                    }
                }
            }

            private static AnatomySlotType ParseSlotType(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return AnatomySlotType.Other;
                }

                var key = value.Trim().ToLowerInvariant();
                if (key.Contains("limb"))
                {
                    return AnatomySlotType.Limb;
                }
                if (key.Contains("organ"))
                {
                    return AnatomySlotType.Organ;
                }
                if (key.Contains("neural") || key.Contains("brain"))
                {
                    return AnatomySlotType.Neural;
                }
                if (key.Contains("sensor") || key.Contains("sense"))
                {
                    return AnatomySlotType.Sensory;
                }
                if (key.Contains("utility") || key.Contains("mount"))
                {
                    return AnatomySlotType.Utility;
                }

                return AnatomySlotType.Other;
            }
        }
    }
}
