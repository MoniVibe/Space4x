using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for lineage/dynasty membership.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Lineage")]
    public sealed class LineageAuthoring : MonoBehaviour
    {
        [Tooltip("Lineage ID (references lineage/dynasty catalog)")]
        public string lineageId = string.Empty;

        public sealed class Baker : Unity.Entities.Baker<LineageAuthoring>
        {
            public override void Bake(LineageAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                if (!string.IsNullOrWhiteSpace(authoring.lineageId))
                {
                    AddComponent(entity, new Registry.LineageId
                    {
                        Id = new FixedString64Bytes(authoring.lineageId)
                    });
                }
            }
        }
    }
}

