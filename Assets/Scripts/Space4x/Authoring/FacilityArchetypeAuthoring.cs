using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for facility archetype (Refinery, Fabricator, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Facility Archetype")]
    public sealed class FacilityArchetypeAuthoring : MonoBehaviour
    {
        [Tooltip("Facility archetype (Refinery, Fabricator, etc.)")]
        public FacilityArchetype facilityArchetype = FacilityArchetype.None;

        public sealed class Baker : Unity.Entities.Baker<FacilityArchetypeAuthoring>
        {
            public override void Bake(FacilityArchetypeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                if (authoring.facilityArchetype != FacilityArchetype.None)
                {
                    AddComponent(entity, new Registry.FacilityArchetypeComponent { Value = authoring.facilityArchetype });
                }
            }
        }
    }
}

