using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for medical shift scheduling policy.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Medical Shift Policy")]
    public sealed class MedicalShiftPolicyAuthoring : MonoBehaviour
    {
        [Min(1f)]
        public float dayLengthHours = 24f;
        [Min(0f)]
        public float clinicStartHour = 7f;
        [Min(0f)]
        public float clinicDurationHours = 10f;
        [Min(0f)]
        public float surgeryStartHour = 9f;
        [Min(0f)]
        public float surgeryDurationHours = 8f;
        [Min(0f)]
        public float researchStartHour = 12f;
        [Min(0f)]
        public float researchDurationHours = 6f;
        [Range(0f, 1f)]
        public float offHoursEfficiency = 0.2f;

        private sealed class Baker : Baker<MedicalShiftPolicyAuthoring>
        {
            public override void Bake(MedicalShiftPolicyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MedicalShiftPolicy
                {
                    DayLengthHours = math.max(1f, authoring.dayLengthHours),
                    ClinicStartHour = math.max(0f, authoring.clinicStartHour),
                    ClinicDurationHours = math.max(0f, authoring.clinicDurationHours),
                    SurgeryStartHour = math.max(0f, authoring.surgeryStartHour),
                    SurgeryDurationHours = math.max(0f, authoring.surgeryDurationHours),
                    ResearchStartHour = math.max(0f, authoring.researchStartHour),
                    ResearchDurationHours = math.max(0f, authoring.researchDurationHours),
                    OffHoursEfficiency = (half)math.clamp(authoring.offHoursEfficiency, 0f, 1f)
                });
            }
        }
    }
}
