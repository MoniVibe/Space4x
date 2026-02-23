using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for medical facility limb data on modules.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Medical Facility Limb")]
    public sealed class MedicalFacilityLimbAuthoring : MonoBehaviour
    {
        [Header("Facility Type")]
        public MedicalFacilityType type = MedicalFacilityType.Clinic;

        [Header("Rates (per second)")]
        public float treatmentRate = 0.02f;
        public float surgeryRate = 0.01f;
        public float recoveryRate = 0.03f;
        public float researchRate = 0.01f;
        public float augmentRate = 0.005f;

        [Header("Modifiers")]
        public float augmentQualityBonus = 0.02f;
        public float infectionRisk = 0.02f;
        public float malpracticeRisk = 0.01f;
        public float sterility = 0.7f;

        private sealed class Baker : Baker<MedicalFacilityLimbAuthoring>
        {
            public override void Bake(MedicalFacilityLimbAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MedicalFacilityLimb
                {
                    Type = authoring.type,
                    TreatmentRate = Mathf.Max(0f, authoring.treatmentRate),
                    SurgeryRate = Mathf.Max(0f, authoring.surgeryRate),
                    RecoveryRate = Mathf.Max(0f, authoring.recoveryRate),
                    ResearchRate = Mathf.Max(0f, authoring.researchRate),
                    AugmentRate = Mathf.Max(0f, authoring.augmentRate),
                    AugmentQualityBonus = Mathf.Max(0f, authoring.augmentQualityBonus),
                    InfectionRisk = Mathf.Max(0f, authoring.infectionRisk),
                    MalpracticeRisk = Mathf.Max(0f, authoring.malpracticeRisk),
                    Sterility = Mathf.Max(0f, authoring.sterility)
                });
            }
        }
    }
}
