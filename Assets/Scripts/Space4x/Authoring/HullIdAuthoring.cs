using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that identifies a hull by its catalog ID.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Hull ID")]
    public sealed class HullIdAuthoring : MonoBehaviour
    {
        [Tooltip("Hull ID from the catalog (e.g., 'lcv-sparrow', 'cv-mule')")]
        public string hullId = string.Empty;

        private void OnValidate()
        {
            hullId = string.IsNullOrWhiteSpace(hullId) ? string.Empty : hullId.Trim();
        }

        public sealed class Baker : Unity.Entities.Baker<HullIdAuthoring>
        {
            public override void Bake(HullIdAuthoring authoring)
            {
                if (string.IsNullOrWhiteSpace(authoring.hullId))
                {
                    Debug.LogWarning($"HullIdAuthoring on '{authoring.name}' has no hullId set.");
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.HullId
                {
                    Id = new FixedString64Bytes(authoring.hullId)
                });
            }
        }
    }
}

