using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for module manufacturer (references manufacturer catalog for signature traits).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module Manufacturer")]
    public sealed class ModuleManufacturerAuthoring : MonoBehaviour
    {
        [Tooltip("Manufacturer ID (references manufacturer catalog for signature traits)")]
        public string manufacturerId = string.Empty;

        public sealed class Baker : Unity.Entities.Baker<ModuleManufacturerAuthoring>
        {
            public override void Bake(ModuleManufacturerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.ModuleManufacturer
                {
                    ManufacturerId = new FixedString64Bytes(authoring.manufacturerId ?? string.Empty)
                });
            }
        }
    }
}

