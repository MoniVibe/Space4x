using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that identifies a resource by its catalog ID.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Resource ID")]
    public sealed class ResourceIdAuthoring : MonoBehaviour
    {
        [Tooltip("Resource ID from the catalog")]
        public string resourceId = string.Empty;

        private void OnValidate()
        {
            resourceId = string.IsNullOrWhiteSpace(resourceId) ? string.Empty : resourceId.Trim();
        }

        public sealed class Baker : Unity.Entities.Baker<ResourceIdAuthoring>
        {
            public override void Bake(ResourceIdAuthoring authoring)
            {
                if (string.IsNullOrWhiteSpace(authoring.resourceId))
                {
                    Debug.LogWarning($"ResourceIdAuthoring on '{authoring.name}' has no resourceId set.");
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.ResourceId
                {
                    Id = new FixedString64Bytes(authoring.resourceId)
                });
            }
        }
    }
}

