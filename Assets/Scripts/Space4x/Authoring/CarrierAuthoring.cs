using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that marks an entity as a carrier.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Carrier")]
    public sealed class CarrierAuthoring : MonoBehaviour
    {
        public sealed class Baker : Unity.Entities.Baker<CarrierAuthoring>
        {
            public override void Bake(CarrierAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<Registry.CarrierTag>(entity);
            }
        }
    }
}

