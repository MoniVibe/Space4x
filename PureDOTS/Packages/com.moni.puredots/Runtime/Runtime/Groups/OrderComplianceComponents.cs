using Unity.Entities;

namespace PureDOTS.Runtime.Groups
{
    public enum OrderKind : byte
    {
        None = 0,
        SquadTactic = 1
    }

    public enum OrderOutcomeKind : byte
    {
        None = 0,
        Obey = 1,
        Refuse = 2,
        EscalatedObey = 3,
        EscalatedRefuse = 4
    }

    public enum OrderRefusalReason : byte
    {
        None = 0,
        WeakClaim = 1,
        LowLegitimacy = 2,
        LowConsent = 3,
        HostileIssuer = 4,
        DispositionMismatch = 5
    }

    /// <summary>
    /// Snapshot of the latest issued order for compliance evaluation and audit.
    /// </summary>
    public struct OrderIssued : IComponentData
    {
        public OrderKind Kind;
        public SquadTacticKind SquadTactic;
        public Entity Issuer;
        public Entity Subject;
        public Entity Target;
        public float FocusBudgetCost;
        public float DisciplineRequired;
        public byte AckMode;
        public uint IssueTick;
        public Entity IssuingSeat;
        public Entity IssuingOccupant;
        public Entity ActingSeat;
        public Entity ActingOccupant;
    }

    /// <summary>
    /// Outcome of compliance evaluation for the latest order.
    /// </summary>
    public struct OrderOutcome : IComponentData
    {
        public OrderKind Kind;
        public SquadTacticKind SquadTactic;
        public OrderOutcomeKind Outcome;
        public OrderRefusalReason Reason;
        public Entity Issuer;
        public Entity Subject;
        public Entity ActingSeat;
        public Entity ActingOccupant;
        public uint IssuedTick;
        public uint DecidedTick;
    }

    /// <summary>
    /// Tracks escalation attempts for orders.
    /// </summary>
    public struct OrderEscalationState : IComponentData
    {
        public byte AttemptCount;
        public uint LastAttemptTick;
        public uint CooldownTicks;
    }

    /// <summary>
    /// Tunable compliance thresholds and weights for orders.
    /// </summary>
    public struct OrderComplianceConfig : IComponentData
    {
        public float TightenThreshold;
        public float LoosenThreshold;
        public float FlankThreshold;
        public float CollapseThreshold;
        public float RetreatThreshold;
        public float PressureWeight;
        public float LegitimacyWeight;
        public float ConsentWeight;
        public float HostilityWeight;
        public float DispositionWeight;
        public float DeterministicBias;
        public float EscalationPressureBonus;
        public float EscalationLegitimacyBonus;
        public byte MaxEscalationAttempts;
        public uint EscalationCooldownTicks;

        public static OrderComplianceConfig Default => new OrderComplianceConfig
        {
            TightenThreshold = 0.55f,
            LoosenThreshold = 0.45f,
            FlankThreshold = 0.6f,
            CollapseThreshold = 0.58f,
            RetreatThreshold = 0.5f,
            PressureWeight = 0.45f,
            LegitimacyWeight = 0.2f,
            ConsentWeight = 0.2f,
            HostilityWeight = 0.25f,
            DispositionWeight = 0.35f,
            DeterministicBias = 0.05f,
            EscalationPressureBonus = 0.25f,
            EscalationLegitimacyBonus = 0.15f,
            MaxEscalationAttempts = 1,
            EscalationCooldownTicks = 30
        };
    }
}
