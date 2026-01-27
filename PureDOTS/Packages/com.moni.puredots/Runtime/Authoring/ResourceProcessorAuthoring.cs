using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using PureDOTS.Runtime.Resource;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class ResourceProcessorAuthoring : MonoBehaviour
    {
        [Tooltip("Facility tag used to match recipes (e.g., refinery, bio_lab, electronics_fab). Leave blank to allow all recipes.")]
        public string facilityTag = string.Empty;

        [Tooltip("When enabled, the processor will automatically run any matching recipe whenever ingredients are available.")]
        public bool autoRun = true;
    }

    public sealed class ResourceProcessorBaker : Baker<ResourceProcessorAuthoring>
    {
        public override void Bake(ResourceProcessorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            FixedString32Bytes facilityTag = default;
            if (!string.IsNullOrWhiteSpace(authoring.facilityTag))
            {
                facilityTag.Append(authoring.facilityTag.Trim());
            }

            AddComponent(entity, new ResourceProcessorConfig
            {
                FacilityTag = facilityTag,
                AutoRun = (byte)(authoring.autoRun ? 1 : 0)
            });

            AddComponent<ResourceProcessorState>(entity);
            AddBuffer<ResourceProcessorQueue>(entity);
        }
    }
}

