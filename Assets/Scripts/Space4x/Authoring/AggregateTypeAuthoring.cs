using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for aggregate type (Dynasty, Guild, Corporation, Army, Band).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Aggregate Type")]
    public sealed class AggregateTypeAuthoring : MonoBehaviour
    {
        [Tooltip("Type of aggregate (Dynasty, Guild, Corporation, Army, Band)")]
        public AffiliationType aggregateType = AffiliationType.Faction;

        public sealed class Baker : Unity.Entities.Baker<AggregateTypeAuthoring>
        {
            public override void Bake(AggregateTypeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.AggregateType { Value = authoring.aggregateType });
            }
        }
    }
}

