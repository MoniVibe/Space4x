using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Profile
{
    /// <summary>
    /// Lightweight behavioral weights for per-entity decision and steering flavor.
    /// Values are normalized [0..1] where 0.5 is neutral.
    /// </summary>
    public struct BehaviorDisposition : IComponentData
    {
        public float Compliance;
        public float Caution;
        public float FormationAdherence;
        public float RiskTolerance;
        public float Aggression;
        public float Patience;

        public static BehaviorDisposition Default => new BehaviorDisposition
        {
            Compliance = 0.5f,
            Caution = 0.5f,
            FormationAdherence = 0.5f,
            RiskTolerance = 0.5f,
            Aggression = 0.5f,
            Patience = 0.5f
        };

        public static BehaviorDisposition FromValues(
            float compliance,
            float caution,
            float formationAdherence,
            float riskTolerance,
            float aggression,
            float patience)
        {
            return new BehaviorDisposition
            {
                Compliance = math.clamp(compliance, 0f, 1f),
                Caution = math.clamp(caution, 0f, 1f),
                FormationAdherence = math.clamp(formationAdherence, 0f, 1f),
                RiskTolerance = math.clamp(riskTolerance, 0f, 1f),
                Aggression = math.clamp(aggression, 0f, 1f),
                Patience = math.clamp(patience, 0f, 1f)
            };
        }
    }
}
