using PureDOTS.Runtime.Profile;
using Unity.Mathematics;

namespace Space4X.SimServer
{
    internal static class Space4XSimServerProfileUtility
    {
        internal static BehaviorDisposition BuildLeaderDisposition(
            float security,
            float economy,
            float research,
            float expansion,
            float diplomacy,
            float aggression,
            float risk,
            float food)
        {
            var compliance = math.saturate(0.3f + diplomacy * 0.5f + economy * 0.1f);
            var caution = math.saturate(0.2f + (1f - risk) * 0.55f + (1f - aggression) * 0.15f + (1f - expansion) * 0.1f);
            var formation = math.saturate(0.25f + security * 0.6f + diplomacy * 0.1f);
            var patience = math.saturate(0.2f + research * 0.45f + economy * 0.2f + food * 0.1f + (1f - expansion) * 0.05f);

            return BehaviorDisposition.FromValues(
                compliance,
                caution,
                formation,
                math.saturate(risk),
                math.saturate(aggression),
                patience);
        }

        internal static BehaviorDisposition LerpDisposition(in BehaviorDisposition current, in BehaviorDisposition target, float t)
        {
            t = math.saturate(t);
            return BehaviorDisposition.FromValues(
                math.lerp(current.Compliance, target.Compliance, t),
                math.lerp(current.Caution, target.Caution, t),
                math.lerp(current.FormationAdherence, target.FormationAdherence, t),
                math.lerp(current.RiskTolerance, target.RiskTolerance, t),
                math.lerp(current.Aggression, target.Aggression, t),
                math.lerp(current.Patience, target.Patience, t));
        }
    }
}
