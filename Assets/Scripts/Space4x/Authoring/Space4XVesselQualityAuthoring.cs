using Space4X.Runtime;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Vessel Quality")]
    public sealed class Space4XVesselQualityAuthoring : MonoBehaviour
    {
        [Range(0f, 1f)] public float hullQuality = 0.5f;
        [Range(0f, 1f)] public float systemsQuality = 0.5f;
        [Range(0f, 1f)] public float mobilityQuality = 0.5f;
        [Range(0f, 1f)] public float integrationQuality = 0.5f;

        public sealed class Baker : Baker<Space4XVesselQualityAuthoring>
        {
            public override void Bake(Space4XVesselQualityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new VesselQuality
                {
                    HullQuality = Mathf.Clamp01(authoring.hullQuality),
                    SystemsQuality = Mathf.Clamp01(authoring.systemsQuality),
                    MobilityQuality = Mathf.Clamp01(authoring.mobilityQuality),
                    IntegrationQuality = Mathf.Clamp01(authoring.integrationQuality)
                });
            }
        }
    }
}
