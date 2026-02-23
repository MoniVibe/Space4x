using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for medical staffing requirements.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Medical Staffing Policy")]
    public sealed class MedicalStaffingPolicyAuthoring : MonoBehaviour
    {
        [Range(0, 255)]
        public byte doctorsRequired = 1;
        [Range(0, 255)]
        public byte surgeonsRequired = 1;
        [Range(0, 255)]
        public byte assistantsRequired = 2;
        [Range(0, 255)]
        public byte researchersRequired = 1;

        private sealed class Baker : Baker<MedicalStaffingPolicyAuthoring>
        {
            public override void Bake(MedicalStaffingPolicyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MedicalStaffingPolicy
                {
                    DoctorsRequired = authoring.doctorsRequired,
                    SurgeonsRequired = authoring.surgeonsRequired,
                    AssistantsRequired = authoring.assistantsRequired,
                    ResearchersRequired = authoring.researchersRequired
                });
            }
        }
    }
}
