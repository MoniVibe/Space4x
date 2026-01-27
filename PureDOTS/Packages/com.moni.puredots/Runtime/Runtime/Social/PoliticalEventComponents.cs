using Unity.Entities;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Alliance consideration event - triggered when conditions cross thresholds.
    /// Evaluated by cold path political systems.
    /// </summary>
    public struct AllianceConsideration : IComponentData
    {
        /// <summary>
        /// Initiator organization.
        /// </summary>
        public Entity InitiatorOrg;

        /// <summary>
        /// Target organization.
        /// </summary>
        public Entity TargetOrg;

        /// <summary>
        /// Current relation attitude.
        /// </summary>
        public float CurrentAttitude;

        /// <summary>
        /// Threat level from other sources.
        /// </summary>
        public float ThreatLevel;

        /// <summary>
        /// Trade volume between orgs.
        /// </summary>
        public float TradeVolume;

        /// <summary>
        /// Tick when consideration was created.
        /// </summary>
        public uint CreatedTick;

        /// <summary>
        /// Priority (0=Critical, 1=Important, 2=Normal, 3=Low).
        /// </summary>
        public byte Priority;
    }

    /// <summary>
    /// Sanction consideration event - triggered when conditions cross thresholds.
    /// Evaluated by cold path political systems.
    /// </summary>
    public struct SanctionConsideration : IComponentData
    {
        /// <summary>
        /// Initiator organization.
        /// </summary>
        public Entity InitiatorOrg;

        /// <summary>
        /// Target organization.
        /// </summary>
        public Entity TargetOrg;

        /// <summary>
        /// Reason for sanction consideration.
        /// </summary>
        public SanctionReason Reason;

        /// <summary>
        /// Current relation attitude.
        /// </summary>
        public float CurrentAttitude;

        /// <summary>
        /// Severity of offense (0..1).
        /// </summary>
        public float OffenseSeverity;

        /// <summary>
        /// Tick when consideration was created.
        /// </summary>
        public uint CreatedTick;

        /// <summary>
        /// Priority (0=Critical, 1=Important, 2=Normal, 3=Low).
        /// </summary>
        public byte Priority;
    }

    /// <summary>
    /// Reason for sanction consideration.
    /// </summary>
    public enum SanctionReason : byte
    {
        None = 0,
        Aggression = 1,
        Atrocity = 2,
        TradeViolation = 3,
        TreatyBreach = 4,
        ReligiousOffense = 5,
        CulturalOffense = 6
    }
}

