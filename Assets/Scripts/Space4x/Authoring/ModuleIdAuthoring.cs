using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Authoring component that identifies a module by its catalog ID.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module ID")]
    public sealed class ModuleIdAuthoring : MonoBehaviour
    {
        [Tooltip("Module ID from the catalog (e.g., 'reactor-mk1', 'laser-s-1')")]
        public string moduleId = string.Empty;

        private void OnValidate()
        {
            moduleId = string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId.Trim();
        }

        public sealed class Baker : Unity.Entities.Baker<ModuleIdAuthoring>
        {
            public override void Bake(ModuleIdAuthoring authoring)
            {
                if (string.IsNullOrWhiteSpace(authoring.moduleId))
                {
                    Debug.LogWarning($"ModuleIdAuthoring on '{authoring.name}' has no moduleId set.");
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.ModuleId
                {
                    Id = new FixedString64Bytes(authoring.moduleId)
                });
            }
        }
    }
}

