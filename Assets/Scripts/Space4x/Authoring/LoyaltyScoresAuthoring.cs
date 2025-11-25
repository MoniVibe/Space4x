using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for loyalty scores (Empire, Lineage, Guild).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Loyalty Scores")]
    public sealed class LoyaltyScoresAuthoring : MonoBehaviour
    {
        [Serializable]
        public class LoyaltyEntry
        {
            [Tooltip("Loyalty target type")]
            public AffiliationType targetType;
            [Tooltip("Target ID (empire, lineage, guild)")]
            public string targetId = string.Empty;
            [Tooltip("Loyalty score in [0, 1]")]
            [Range(0f, 1f)]
            public float loyalty = 0.5f;
        }

        [Tooltip("Loyalty scores (multiple allowed)")]
        public List<LoyaltyEntry> loyaltyScores = new List<LoyaltyEntry>();

        public sealed class Baker : Unity.Entities.Baker<LoyaltyScoresAuthoring>
        {
            public override void Bake(LoyaltyScoresAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<Registry.LoyaltyScore>(entity);

                if (authoring.loyaltyScores != null)
                {
                    foreach (var entry in authoring.loyaltyScores)
                    {
                        if (!string.IsNullOrWhiteSpace(entry.targetId))
                        {
                            buffer.Add(new Registry.LoyaltyScore
                            {
                                TargetType = entry.targetType,
                                TargetId = new FixedString64Bytes(entry.targetId),
                                Loyalty = (half)math.clamp(entry.loyalty, 0f, 1f)
                            });
                        }
                    }
                }
            }
        }
    }
}

