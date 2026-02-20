using PureDOTS.Runtime.Profile;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Marks a pilot as managed by FleetCrawl pilot directive/trust systems.
    /// </summary>
    public struct Space4XPilotManagedTag : IComponentData { }

    /// <summary>
    /// Explicit player/managerial bias values in [-1, +1].
    /// Positive aggression/risk pushes bolder behavior.
    /// Positive caution/compliance/formation/patience pushes safer disciplined behavior.
    /// </summary>
    public struct Space4XPilotDirective : IComponentData
    {
        public half AggressionBias;
        public half CautionBias;
        public half ComplianceBias;
        public half FormationBias;
        public half RiskBias;
        public half PatienceBias;

        public static Space4XPilotDirective Neutral => new Space4XPilotDirective
        {
            AggressionBias = (half)0f,
            CautionBias = (half)0f,
            ComplianceBias = (half)0f,
            FormationBias = (half)0f,
            RiskBias = (half)0f,
            PatienceBias = (half)0f
        };
    }

    /// <summary>
    /// Trust channels in [-1, +1] used to bias compliance/formation and volatility.
    /// </summary>
    public struct Space4XPilotTrust : IComponentData
    {
        public half CommandTrust;
        public half CrewTrust;

        public static Space4XPilotTrust Neutral => new Space4XPilotTrust
        {
            CommandTrust = (half)0f,
            CrewTrust = (half)0f
        };
    }

    /// <summary>
    /// Immutable baseline copied from the pilot profile before directive/trust offsets are applied.
    /// </summary>
    public struct Space4XPilotBehaviorBaseline : IComponentData
    {
        public BehaviorDisposition Value;
        public uint CapturedTick;
    }

    /// <summary>
    /// Runtime effective behavior and pressure diagnostics for telemetry.
    /// </summary>
    public struct Space4XPilotBehaviorRuntime : IComponentData
    {
        public BehaviorDisposition Effective;
        public float DirectivePressure;
        public float TrustPressure;
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Tuning for how directives/trust/morale alter pilot behavior from baseline.
    /// </summary>
    public struct Space4XPilotManagerConfig : IComponentData
    {
        public float DirectiveWeight;
        public float TrustComplianceWeight;
        public float TrustFormationWeight;
        public float TrustPatienceWeight;
        public float LowTrustAggressionWeight;
        public float LowTrustCautionWeight;
        public float LowTrustRiskWeight;
        public float MoraleWeight;

        public static Space4XPilotManagerConfig Default => new Space4XPilotManagerConfig
        {
            DirectiveWeight = 0.18f,
            TrustComplianceWeight = 0.2f,
            TrustFormationWeight = 0.18f,
            TrustPatienceWeight = 0.08f,
            LowTrustAggressionWeight = 0.14f,
            LowTrustCautionWeight = 0.1f,
            LowTrustRiskWeight = 0.12f,
            MoraleWeight = 0.1f
        };
    }
}
