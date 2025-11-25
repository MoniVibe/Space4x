using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for module tier (drives baseline performance, reliability).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module Tier")]
    public sealed class ModuleTierAuthoring : MonoBehaviour
    {
        [Tooltip("Tier (0-255). Drives baseline performance, reliability")]
        [Range(0, 255)]
        public byte tier = 1;

        public sealed class Baker : Unity.Entities.Baker<ModuleTierAuthoring>
        {
            public override void Bake(ModuleTierAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.ModuleTier { Value = authoring.tier });
            }
        }
    }
}

