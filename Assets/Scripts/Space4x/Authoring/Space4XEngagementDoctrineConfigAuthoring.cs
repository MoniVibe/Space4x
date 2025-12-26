using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Engagement Doctrine Config")]
    public sealed class Space4XEngagementDoctrineConfigAuthoring : MonoBehaviour
    {
        [Header("Update Cadence (Ticks)")]
        [SerializeField, Range(1f, 300f)] private float threatUpdateIntervalTicks = 10f;
        [SerializeField, Range(1f, 600f)] private float intentUpdateIntervalTicks = 30f;
        [SerializeField, Range(1f, 300f)] private float tacticUpdateIntervalTicks = 15f;
        [SerializeField, Range(1f, 32f)] private float threatSampleLimit = 8f;

        [Header("Escape")]
        [SerializeField, Min(1f)] private float escapeDistance = 150f;
        [SerializeField, Range(0f, 1f)] private float minEscapeProbability = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float retreatRangeScale = 0.6f;

        [Header("Advantage Thresholds")]
        [SerializeField, Range(0.1f, 2.5f)] private float fightAdvantageThreshold = 1.1f;
        [SerializeField, Range(0.1f, 2.5f)] private float harassAdvantageThreshold = 0.9f;
        [SerializeField, Range(0.1f, 2.5f)] private float retreatAdvantageThreshold = 0.7f;
        [SerializeField, Range(0.1f, 2.5f)] private float breakthroughAdvantageThreshold = 0.6f;

        [Header("Disposition Biases")]
        [SerializeField, Range(0f, 1f)] private float aggressionFightBias = 0.25f;
        [SerializeField, Range(0f, 1f)] private float cautionFightBias = 0.2f;
        [SerializeField, Range(0f, 1f)] private float aggressionRetreatBias = 0.2f;
        [SerializeField, Range(0f, 1f)] private float cautionRetreatBias = 0.3f;
        [SerializeField, Range(0f, 1f)] private float riskBreakthroughBias = 0.25f;
        [SerializeField, Range(0f, 1f)] private float patienceHarassBias = 0.2f;

        [Header("Tactics")]
        [SerializeField, Range(0f, 1f)] private float disciplineTightenThreshold = 0.55f;
        [SerializeField, Range(0f, 1f)] private float aggressionFlankThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] private float cohesionFlankThreshold = 0.55f;
        [SerializeField] private bool allowBreakthrough = false;

        private void OnValidate()
        {
            threatUpdateIntervalTicks = math.clamp(threatUpdateIntervalTicks, 1f, 300f);
            intentUpdateIntervalTicks = math.clamp(intentUpdateIntervalTicks, 1f, 600f);
            tacticUpdateIntervalTicks = math.clamp(tacticUpdateIntervalTicks, 1f, 300f);
            threatSampleLimit = math.clamp(threatSampleLimit, 1f, 32f);
            escapeDistance = math.max(1f, escapeDistance);
            minEscapeProbability = math.clamp(minEscapeProbability, 0f, 1f);
            retreatRangeScale = math.clamp(retreatRangeScale, 0.1f, 1f);
            fightAdvantageThreshold = math.clamp(fightAdvantageThreshold, 0.1f, 2.5f);
            harassAdvantageThreshold = math.clamp(harassAdvantageThreshold, 0.1f, 2.5f);
            retreatAdvantageThreshold = math.clamp(retreatAdvantageThreshold, 0.1f, 2.5f);
            breakthroughAdvantageThreshold = math.clamp(breakthroughAdvantageThreshold, 0.1f, 2.5f);
            aggressionFightBias = math.clamp(aggressionFightBias, 0f, 1f);
            cautionFightBias = math.clamp(cautionFightBias, 0f, 1f);
            aggressionRetreatBias = math.clamp(aggressionRetreatBias, 0f, 1f);
            cautionRetreatBias = math.clamp(cautionRetreatBias, 0f, 1f);
            riskBreakthroughBias = math.clamp(riskBreakthroughBias, 0f, 1f);
            patienceHarassBias = math.clamp(patienceHarassBias, 0f, 1f);
            disciplineTightenThreshold = math.clamp(disciplineTightenThreshold, 0f, 1f);
            aggressionFlankThreshold = math.clamp(aggressionFlankThreshold, 0f, 1f);
            cohesionFlankThreshold = math.clamp(cohesionFlankThreshold, 0f, 1f);
        }

        private sealed class Baker : Baker<Space4XEngagementDoctrineConfigAuthoring>
        {
            public override void Bake(Space4XEngagementDoctrineConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EngagementDoctrineConfig
                {
                    ThreatUpdateIntervalTicks = (uint)math.max(1f, authoring.threatUpdateIntervalTicks),
                    IntentUpdateIntervalTicks = (uint)math.max(1f, authoring.intentUpdateIntervalTicks),
                    TacticUpdateIntervalTicks = (uint)math.max(1f, authoring.tacticUpdateIntervalTicks),
                    ThreatSampleLimit = (byte)math.clamp(math.round(authoring.threatSampleLimit), 1f, 32f),
                    EscapeDistance = math.max(1f, authoring.escapeDistance),
                    MinEscapeProbability = math.clamp(authoring.minEscapeProbability, 0f, 1f),
                    FightAdvantageThreshold = math.clamp(authoring.fightAdvantageThreshold, 0.1f, 2.5f),
                    HarassAdvantageThreshold = math.clamp(authoring.harassAdvantageThreshold, 0.1f, 2.5f),
                    RetreatAdvantageThreshold = math.clamp(authoring.retreatAdvantageThreshold, 0.1f, 2.5f),
                    BreakthroughAdvantageThreshold = math.clamp(authoring.breakthroughAdvantageThreshold, 0.1f, 2.5f),
                    AggressionFightBias = math.clamp(authoring.aggressionFightBias, 0f, 1f),
                    CautionFightBias = math.clamp(authoring.cautionFightBias, 0f, 1f),
                    AggressionRetreatBias = math.clamp(authoring.aggressionRetreatBias, 0f, 1f),
                    CautionRetreatBias = math.clamp(authoring.cautionRetreatBias, 0f, 1f),
                    RiskBreakthroughBias = math.clamp(authoring.riskBreakthroughBias, 0f, 1f),
                    PatienceHarassBias = math.clamp(authoring.patienceHarassBias, 0f, 1f),
                    DisciplineTightenThreshold = math.clamp(authoring.disciplineTightenThreshold, 0f, 1f),
                    AggressionFlankThreshold = math.clamp(authoring.aggressionFlankThreshold, 0f, 1f),
                    CohesionFlankThreshold = math.clamp(authoring.cohesionFlankThreshold, 0f, 1f),
                    RetreatRangeScale = math.clamp(authoring.retreatRangeScale, 0.1f, 1f),
                    AllowBreakthrough = (byte)(authoring.allowBreakthrough ? 1 : 0)
                });
            }
        }
    }
}
