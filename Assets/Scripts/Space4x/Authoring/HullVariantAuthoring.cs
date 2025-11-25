using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for hull variant (Common, Uncommon, Heroic, Prototype).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Hull Variant")]
    public sealed class HullVariantAuthoring : MonoBehaviour
    {
        [Tooltip("Hull variant (Common, Uncommon, Heroic, Prototype)")]
        public HullVariant variant = HullVariant.Common;

        public sealed class Baker : Unity.Entities.Baker<HullVariantAuthoring>
        {
            public override void Bake(HullVariantAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.HullVariantComponent { Value = authoring.variant });
            }
        }
    }
}

