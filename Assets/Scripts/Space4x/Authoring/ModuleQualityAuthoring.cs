using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for module quality (fine control over spread/dispersion, misfire risk, maintenance load).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module Quality")]
    public sealed class ModuleQualityAuthoring : MonoBehaviour
    {
        [Tooltip("Quality in [0, 1]. Fine control over spread/dispersion, misfire risk, maintenance load")]
        [Range(0f, 1f)]
        public float quality = 1f;

        public sealed class Baker : Unity.Entities.Baker<ModuleQualityAuthoring>
        {
            public override void Bake(ModuleQualityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.ModuleQuality { Value = math.clamp(authoring.quality, 0f, 1f) });
            }
        }
    }
}

