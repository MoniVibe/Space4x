using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for affiliation tags (membership in organizations).
    /// Note: Target entities are resolved at runtime, so we store target IDs as strings.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Affiliation")]
    public sealed class AffiliationAuthoring : MonoBehaviour
    {
        [Serializable]
        public class AffiliationEntry
        {
            public AffiliationType type;
            [Tooltip("Target entity ID (resolved at runtime)")]
            public string targetId = string.Empty;
            [Tooltip("Loyalty in [0, 1]. Moderates mutiny/desertion severity")]
            [Range(0f, 1f)]
            public float loyalty = 0.5f;
        }

        [Tooltip("Affiliation memberships (multiple allowed)")]
        public List<AffiliationEntry> affiliations = new List<AffiliationEntry>();

        public sealed class Baker : Unity.Entities.Baker<AffiliationAuthoring>
        {
            public override void Bake(AffiliationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<AffiliationTag>(entity);

                if (authoring.affiliations != null)
                {
                    foreach (var entry in authoring.affiliations)
                    {
                        // Note: Target entity resolution happens at runtime
                        // We store the target ID for later resolution
                        // For now, we create a placeholder entity reference
                        // Runtime systems should resolve targetId → Entity
                        buffer.Add(new AffiliationTag
                        {
                            Type = entry.type,
                            Target = Entity.Null, // Resolved at runtime from targetId
                            Loyalty = (half)math.clamp(entry.loyalty, 0f, 1f)
                        });
                    }
                }
            }
        }
    }
}

