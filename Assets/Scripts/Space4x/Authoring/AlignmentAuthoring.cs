using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for alignment triplet (Law, Good, Integrity).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Alignment")]
    public sealed class AlignmentAuthoring : MonoBehaviour
    {
        [Tooltip("Lawfulness: +1 lawful, -1 chaotic")]
        [Range(-1f, 1f)]
        public float law = 0f;

        [Tooltip("Altruism: +1 good, -1 evil")]
        [Range(-1f, 1f)]
        public float good = 0f;

        [Tooltip("Integrity: +1 pure, -1 corrupt")]
        [Range(-1f, 1f)]
        public float integrity = 0f;

        public sealed class Baker : Unity.Entities.Baker<AlignmentAuthoring>
        {
            public override void Bake(AlignmentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, AlignmentTriplet.FromFloats(authoring.law, authoring.good, authoring.integrity));
            }
        }
    }
}

