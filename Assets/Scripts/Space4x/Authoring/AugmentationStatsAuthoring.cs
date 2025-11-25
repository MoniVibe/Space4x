using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for aggregated augmentation stats (Physique/Finesse/Will modifiers).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Augmentation Stats")]
    public sealed class AugmentationStatsAuthoring : MonoBehaviour
    {
        [Header("Stat Modifiers (aggregated from installed augments)")]
        [Tooltip("Physique modifier")]
        public float physiqueModifier = 0f;
        [Tooltip("Finesse modifier")]
        public float finesseModifier = 0f;
        [Tooltip("Will modifier")]
        public float willModifier = 0f;
        [Tooltip("General modifier")]
        public float generalModifier = 0f;
        [Header("Metadata")]
        [Tooltip("Total upkeep cost")]
        public float totalUpkeepCost = 0f;
        [Tooltip("Aggregated risk factor")]
        [Range(0f, 1f)]
        public float aggregatedRiskFactor = 0f;

        public sealed class Baker : Unity.Entities.Baker<AugmentationStatsAuthoring>
        {
            public override void Bake(AugmentationStatsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.AugmentationStats
                {
                    PhysiqueModifier = authoring.physiqueModifier,
                    FinesseModifier = authoring.finesseModifier,
                    WillModifier = authoring.willModifier,
                    GeneralModifier = authoring.generalModifier,
                    TotalUpkeepCost = authoring.totalUpkeepCost,
                    AggregatedRiskFactor = math.clamp(authoring.aggregatedRiskFactor, 0f, 1f)
                });
            }
        }
    }
}

