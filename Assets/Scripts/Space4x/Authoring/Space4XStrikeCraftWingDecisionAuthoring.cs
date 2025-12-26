using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Strike Craft Wing Decision Config")]
    public sealed class Space4XStrikeCraftWingDecisionAuthoring : MonoBehaviour
    {
        [Header("Decision Cadence")]
        [Min(1f)] public float decisionCooldownTicks = 60f;

        [Header("Wing Size")]
        [Range(2f, 12f)] public float maxWingSize = 6f;

        [Header("Break Thresholds")]
        [Range(0f, 1f)] public float chaosBreakThreshold = 0.55f;
        [Range(0f, 1f)] public float chaosBreakAggressiveThreshold = 0.45f;

        [Header("Form Thresholds")]
        [Range(0f, 1f)] public float lawfulnessFormThreshold = 0.55f;

        public sealed class Baker : Baker<Space4XStrikeCraftWingDecisionAuthoring>
        {
            public override void Bake(Space4XStrikeCraftWingDecisionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new StrikeCraftWingDecisionConfig
                {
                    DecisionCooldownTicks = (uint)Mathf.Max(1f, authoring.decisionCooldownTicks),
                    MaxWingSize = (byte)Mathf.Clamp(Mathf.RoundToInt(authoring.maxWingSize), 2, 12),
                    ChaosBreakThreshold = Mathf.Clamp01(authoring.chaosBreakThreshold),
                    ChaosBreakAggressiveThreshold = Mathf.Clamp01(authoring.chaosBreakAggressiveThreshold),
                    LawfulnessFormThreshold = Mathf.Clamp01(authoring.lawfulnessFormThreshold)
                });
            }
        }
    }
}
