using System;
using UnityEngine;

namespace Space4X.Registry
{
    [CreateAssetMenu(fileName = "Space4XMedicalResearchCatalog", menuName = "Space4X/Registry/Medical Research Catalog")]
    public sealed class Space4XMedicalResearchCatalog : ScriptableObject
    {
        public const string ResourcePath = "Registry/Space4XMedicalResearchCatalog";

        [SerializeField] private MedicalResearchUnlockDefinition[] unlocks = Array.Empty<MedicalResearchUnlockDefinition>();

        public MedicalResearchUnlockDefinition[] Unlocks => unlocks;

        public static Space4XMedicalResearchCatalog LoadOrFallback()
        {
            var catalog = Resources.Load<Space4XMedicalResearchCatalog>(ResourcePath);
            if (catalog == null)
            {
                catalog = CreateRuntimeFallback();
            }

            return catalog;
        }

        public static Space4XMedicalResearchCatalog CreateRuntimeFallback()
        {
            var catalog = CreateInstance<Space4XMedicalResearchCatalog>();
            catalog.ApplyRuntimeDefaults();
            return catalog;
        }

        public void ApplyRuntimeDefaults()
        {
            unlocks = new[]
            {
                new MedicalResearchUnlockDefinition
                {
                    Id = "unlock.limb.medical_clinic",
                    Kind = MedicalUnlockKind.FacilityLimb,
                    TargetId = "limb.medical_clinic",
                    RequiredKnowledge = 40f,
                    Tags = new[] { "medical", "treatment" }
                },
                new MedicalResearchUnlockDefinition
                {
                    Id = "unlock.limb.recovery_ward",
                    Kind = MedicalUnlockKind.FacilityLimb,
                    TargetId = "limb.recovery_ward",
                    RequiredKnowledge = 65f,
                    Tags = new[] { "medical", "recovery" }
                },
                new MedicalResearchUnlockDefinition
                {
                    Id = "unlock.limb.surgical_theater",
                    Kind = MedicalUnlockKind.FacilityLimb,
                    TargetId = "limb.surgical_theater",
                    RequiredKnowledge = 85f,
                    Tags = new[] { "medical", "surgery" }
                },
                new MedicalResearchUnlockDefinition
                {
                    Id = "unlock.limb.med_research_lab",
                    Kind = MedicalUnlockKind.FacilityLimb,
                    TargetId = "limb.med_research_lab",
                    RequiredKnowledge = 115f,
                    Tags = new[] { "medical", "research" }
                },
                new MedicalResearchUnlockDefinition
                {
                    Id = "unlock.limb.augment_bioforge",
                    Kind = MedicalUnlockKind.FacilityLimb,
                    TargetId = "limb.augment_bioforge",
                    RequiredKnowledge = 140f,
                    Tags = new[] { "medical", "augmentation" }
                },
                new MedicalResearchUnlockDefinition
                {
                    Id = "unlock.augment.neural_weave",
                    Kind = MedicalUnlockKind.Augmentation,
                    TargetId = "augment.neural_weave",
                    RequiredKnowledge = 160f,
                    Tags = new[] { "augmentation", "neural" }
                }
            };
        }
    }

    [Serializable]
    public struct MedicalResearchUnlockDefinition
    {
        public string Id;
        public MedicalUnlockKind Kind;
        public string TargetId;
        public float RequiredKnowledge;
        public string[] Tags;
    }
}
