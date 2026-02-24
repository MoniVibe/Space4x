using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for medical care output tuning.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Medical Care Policy")]
    public sealed class MedicalCarePolicyAuthoring : MonoBehaviour
    {
        public float conditionHealScalar = 0.06f;
        public float moduleRepairScalar = 0.4f;
        public float surgeryBonusScalar = 1.35f;
        public float researchScalar = 1f;

        private sealed class Baker : Baker<MedicalCarePolicyAuthoring>
        {
            public override void Bake(MedicalCarePolicyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MedicalCarePolicy
                {
                    ConditionHealScalar = Mathf.Max(0f, authoring.conditionHealScalar),
                    ModuleRepairScalar = Mathf.Max(0f, authoring.moduleRepairScalar),
                    SurgeryBonusScalar = Mathf.Max(0f, authoring.surgeryBonusScalar),
                    ResearchScalar = Mathf.Max(0f, authoring.researchScalar)
                });
            }
        }
    }
}
