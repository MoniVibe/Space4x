using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Spatial;

namespace PureDOTS.Authoring.Spatial
{
    /// <summary>
    /// Opt-in authoring to include this entity in the PureDOTS spatial grid.
    /// Requires a <see cref="Unity.Transforms.LocalTransform" /> at runtime (provided by TransformUsageFlags).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialPresenceAuthoring : MonoBehaviour
    {
        private sealed class Baker : Baker<SpatialPresenceAuthoring>
        {
            public override void Bake(SpatialPresenceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<SpatialIndexedTag>(entity);
            }
        }
    }
}




