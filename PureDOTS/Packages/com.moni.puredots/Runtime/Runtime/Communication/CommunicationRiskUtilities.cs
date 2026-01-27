using Unity.Mathematics;

namespace PureDOTS.Runtime.Communication
{
    public static class CommunicationRiskUtilities
    {
        public const float HighRiskThreshold = 0.7f;

        public static float GetOrderRisk(CommOrderVerb verb)
        {
            return verb switch
            {
                CommOrderVerb.Spearhead => 0.9f,
                CommOrderVerb.DrawFire => 0.85f,
                CommOrderVerb.Attack => 0.8f,
                CommOrderVerb.Retreat => 0.75f,
                CommOrderVerb.FocusFire => 0.7f,
                CommOrderVerb.Flank => 0.6f,
                CommOrderVerb.Suppress => 0.55f,
                CommOrderVerb.Regroup => 0.5f,
                CommOrderVerb.Screen => 0.45f,
                CommOrderVerb.MoveTo => 0.4f,
                CommOrderVerb.Defend => 0.35f,
                CommOrderVerb.Patrol => 0.3f,
                CommOrderVerb.Hold => 0.2f,
                _ => 0f
            };
        }

        public static bool IsHighRisk(CommOrderVerb verb)
        {
            return GetOrderRisk(verb) >= HighRiskThreshold;
        }

        public static float ResolveActThreshold(in CommDecisionConfig config, float risk)
        {
            return math.lerp(config.ActThresholdLowRisk, config.ActThresholdHighRisk, math.saturate(risk));
        }
    }
}
