using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for facility tier (Small, Medium, Large, Massive, Titanic).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Facility Tier")]
    public sealed class FacilityTierAuthoring : MonoBehaviour
    {
        [Tooltip("Facility tier (Small, Medium, Large, Massive, Titanic)")]
        public FacilityTier facilityTier = FacilityTier.Small;

        public sealed class Baker : Unity.Entities.Baker<FacilityTierAuthoring>
        {
            public override void Bake(FacilityTierAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.FacilityTierComponent { Value = authoring.facilityTier });
            }
        }
    }
}

