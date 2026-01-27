using PureDOTS.Runtime.Profile;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PureDOTS/Behavior/Behavior Disposition Seed Config")]
    public sealed class BehaviorDispositionSeedConfigAuthoring : MonoBehaviour
    {
        [Header("Default Ranges (0..1)")]
        [Range(0f, 1f)] public float complianceMin = 0.35f;
        [Range(0f, 1f)] public float complianceMax = 0.65f;
        [Range(0f, 1f)] public float cautionMin = 0.35f;
        [Range(0f, 1f)] public float cautionMax = 0.65f;
        [Range(0f, 1f)] public float formationAdherenceMin = 0.35f;
        [Range(0f, 1f)] public float formationAdherenceMax = 0.65f;
        [Range(0f, 1f)] public float riskToleranceMin = 0.35f;
        [Range(0f, 1f)] public float riskToleranceMax = 0.65f;
        [Range(0f, 1f)] public float aggressionMin = 0.35f;
        [Range(0f, 1f)] public float aggressionMax = 0.65f;
        [Range(0f, 1f)] public float patienceMin = 0.35f;
        [Range(0f, 1f)] public float patienceMax = 0.65f;

        [Header("Seed")]
        [Min(0f)] public uint seedSalt = 0u;

        private void OnValidate()
        {
            NormalizeRange(ref complianceMin, ref complianceMax);
            NormalizeRange(ref cautionMin, ref cautionMax);
            NormalizeRange(ref formationAdherenceMin, ref formationAdherenceMax);
            NormalizeRange(ref riskToleranceMin, ref riskToleranceMax);
            NormalizeRange(ref aggressionMin, ref aggressionMax);
            NormalizeRange(ref patienceMin, ref patienceMax);
        }

        private static void NormalizeRange(ref float min, ref float max)
        {
            min = math.clamp(min, 0f, 1f);
            max = math.clamp(max, 0f, 1f);
            if (min > max)
            {
                (min, max) = (max, min);
            }
        }
    }

    public sealed class BehaviorDispositionSeedConfigBaker : Baker<BehaviorDispositionSeedConfigAuthoring>
    {
        public override void Bake(BehaviorDispositionSeedConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BehaviorDispositionSeedConfig
            {
                Distribution = new BehaviorDispositionDistribution
                {
                    Compliance = new float2(authoring.complianceMin, authoring.complianceMax),
                    Caution = new float2(authoring.cautionMin, authoring.cautionMax),
                    FormationAdherence = new float2(authoring.formationAdherenceMin, authoring.formationAdherenceMax),
                    RiskTolerance = new float2(authoring.riskToleranceMin, authoring.riskToleranceMax),
                    Aggression = new float2(authoring.aggressionMin, authoring.aggressionMax),
                    Patience = new float2(authoring.patienceMin, authoring.patienceMax)
                },
                SeedSalt = authoring.seedSalt
            });
        }
    }
}
