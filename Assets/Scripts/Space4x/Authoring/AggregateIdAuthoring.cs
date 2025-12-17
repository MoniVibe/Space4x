using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Authoring component that identifies an aggregate (faction/outlook/alignment group) by its catalog ID.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Aggregate ID")]
    public sealed class AggregateIdAuthoring : MonoBehaviour
    {
        [Tooltip("Aggregate ID from the catalog")]
        public string aggregateId = string.Empty;

        [Header("Alignment/Outlook/Policy Tokens")]
        [Range(0, 255)]
        [Tooltip("Alignment token")]
        public byte alignment = 0;

        [Range(0, 255)]
        [Tooltip("Outlook token")]
        public byte outlook = 0;

        [Range(0, 255)]
        [Tooltip("Policy token")]
        public byte policy = 0;

        private void OnValidate()
        {
            aggregateId = string.IsNullOrWhiteSpace(aggregateId) ? string.Empty : aggregateId.Trim();
        }

        public sealed class Baker : Unity.Entities.Baker<AggregateIdAuthoring>
        {
            public override void Bake(AggregateIdAuthoring authoring)
            {
                if (string.IsNullOrWhiteSpace(authoring.aggregateId))
                {
                    UnityDebug.LogWarning($"AggregateIdAuthoring on '{authoring.name}' has no aggregateId set.");
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.AggregateId
                {
                    Id = new FixedString64Bytes(authoring.aggregateId)
                });

                AddComponent(entity, new Registry.AggregateTags
                {
                    Alignment = authoring.alignment,
                    Outlook = authoring.outlook,
                    Policy = authoring.policy
                });
            }
        }
    }
}

