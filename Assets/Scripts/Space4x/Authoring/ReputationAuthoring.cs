using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for reputation and prestige scores.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Reputation")]
    public sealed class ReputationAuthoring : MonoBehaviour
    {
        [Tooltip("Reputation score in [0, 1]")]
        [Range(0f, 1f)]
        public float reputation = 0.5f;

        [Tooltip("Prestige score in [0, 1]")]
        [Range(0f, 1f)]
        public float prestige = 0f;

        public sealed class Baker : Unity.Entities.Baker<ReputationAuthoring>
        {
            public override void Bake(ReputationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.Reputation
                {
                    ReputationScore = (half)math.clamp(authoring.reputation, 0f, 1f),
                    PrestigeScore = (half)math.clamp(authoring.prestige, 0f, 1f)
                });
            }
        }
    }
}

