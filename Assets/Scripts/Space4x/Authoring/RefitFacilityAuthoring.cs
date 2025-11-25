using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    public sealed class RefitFacilityAuthoring : MonoBehaviour
    {
        [Header("Facility Zone")]
        [Tooltip("Radius in meters for refit facility zone")]
        [Min(1f)]
        public float radiusMeters = 40f;

        public sealed class Baker : Unity.Entities.Baker<RefitFacilityAuthoring>
        {
            public override void Bake(RefitFacilityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<RefitFacilityTag>(entity);
                AddComponent(entity, new FacilityZone
                {
                    RadiusMeters = math.max(1f, authoring.radiusMeters)
                });
            }
        }
    }
}

