using Unity.Entities;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Pre-computed loyalty state for hot path AI decisions.
    /// AI systems read these scalars directly without graph traversals or recomputation.
    /// </summary>
    public struct LoyaltyState : IComponentData
    {
        /// <summary>
        /// Loyalty to band (0..1).
        /// </summary>
        public float ToBand;

        /// <summary>
        /// Loyalty to faction (0..1).
        /// </summary>
        public float ToFaction;

        /// <summary>
        /// Loyalty to empire (0..1).
        /// </summary>
        public float ToEmpire;

        /// <summary>
        /// Pre-scored betrayal risk (0..1, higher = more likely to betray).
        /// </summary>
        public float BetrayalRisk;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Cached org-to-org standing snapshot for hot path AI decisions.
    /// Used by orgs to quickly evaluate relationships without traversing relation graphs.
    /// </summary>
    public struct OrgStandingSnapshot : IComponentData
    {
        /// <summary>
        /// Target org entity.
        /// </summary>
        public Entity TargetOrg;

        /// <summary>
        /// Attitude toward target org (-100..+100).
        /// </summary>
        public float Attitude;

        /// <summary>
        /// Trust level (0..1).
        /// </summary>
        public float Trust;

        /// <summary>
        /// Fear level (0..1).
        /// </summary>
        public float Fear;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }
}

