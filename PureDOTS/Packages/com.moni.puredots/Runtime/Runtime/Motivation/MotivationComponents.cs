using Unity.Entities;

namespace PureDOTS.Runtime.Motivation
{
    /// <summary>
    /// How this entity tends to act on goals & how strongly it favors aggregate loyalties.
    /// Lives on any entity that can have motivations (person, village, guild, fleet, empire, etc.).
    /// </summary>
    public struct MotivationDrive : IComponentData
    {
        /// <summary>
        /// 0–200. 100 = baseline. Higher = more likely to pursue goals each check.
        /// </summary>
        public byte InitiativeCurrent;

        /// <summary>
        /// 0–200. Ceiling for recovering InitiativeCurrent.
        /// </summary>
        public byte InitiativeMax;

        /// <summary>
        /// 0–200. How strongly the entity foregrounds its primary loyalty:
        /// 0 = no loyalty, 200 = martyr willing to die for aggregate.
        /// </summary>
        public byte LoyaltyCurrent;

        /// <summary>
        /// 0–200. Ceiling / "loyalty capacity".
        /// </summary>
        public byte LoyaltyMax;

        /// <summary>
        /// The aggregate (village, guild, fleet, empire) this entity feels most loyal to.
        /// Entity.Null if none.
        /// </summary>
        public Entity PrimaryLoyaltyTarget;

        /// <summary>
        /// Last sim tick we ran an initiative check; helps rate-limit behavior.
        /// </summary>
        public uint LastInitiativeTick;
    }

    /// <summary>
    /// A single dream/aspiration/desire/ambition/wish slot on an entity.
    /// Interpretation of SpecId is via MotivationCatalog.
    /// </summary>
    [InternalBufferCapacity(5)]
    public struct MotivationSlot : IBufferElementData
    {
        /// <summary>Layer/type of this motivation.</summary>
        public MotivationLayer Layer;

        /// <summary>Current status of this slot.</summary>
        public MotivationStatus Status;

        /// <summary>Lock flags preventing reroll/abandonment.</summary>
        public MotivationLockFlags LockFlags;

        /// <summary>
        /// Game-specific spec id, interpreted via MotivationCatalog blob.
        /// -1 means "no goal assigned".
        /// </summary>
        public short SpecId;

        /// <summary>
        /// 0–255. Heavier = entity cares more. This mixes with Initiative/Loyalty.
        /// </summary>
        public byte Importance;

        /// <summary>
        /// 0–255 ~ 0–100%. Games can use coarse progress if they want.
        /// </summary>
        public byte Progress;

        /// <summary>
        /// When this specific goal started being tracked.
        /// </summary>
        public uint StartedTick;

        /// <summary>
        /// Optional primary target (enemy, lover, village, planet, guild, etc).
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Optional numeric parameters interpreted by game-side systems.
        /// Examples:
        /// - stat index, threshold
        /// - relation type, threshold
        /// - count (number of children, number of colonies)
        /// </summary>
        public int Param0;

        /// <summary>
        /// Additional optional numeric parameter.
        /// </summary>
        public int Param1;
    }

    /// <summary>
    /// Current active goal this entity is trying to act on.
    /// </summary>
    public struct MotivationIntent : IComponentData
    {
        /// <summary>
        /// Index into MotivationSlot buffer, or 255 if none.
        /// </summary>
        public byte ActiveSlotIndex;

        /// <summary>Layer of the active goal.</summary>
        public MotivationLayer ActiveLayer;

        /// <summary>
        /// Spec id cached for convenience; -1 if none.
        /// </summary>
        public short ActiveSpecId;
    }

    /// <summary>
    /// Rewards accumulated by fulfilling locked goals.
    /// Used for dynasty/legacy/crew lineage boosts.
    /// </summary>
    public struct LegacyPoints : IComponentData
    {
        /// <summary>
        /// Total dynasty/legacy points ever earned.
        /// </summary>
        public int TotalEarned;

        /// <summary>
        /// Points available for AI to spend on bloodline / dynasty / crew boosts.
        /// </summary>
        public int Unspent;
    }

    /// <summary>
    /// Moral/temperament axes used to bias motivation selection & generation.
    /// Range for each axis: -100..100 (0 = neutral).
    /// Extended with social traits for aggregate dynamics.
    /// </summary>
    public struct MoralProfile : IComponentData
    {
        /// <summary>-100 = totally corrupt (self first), +100 = totally pure (loyalty first).</summary>
        public sbyte CorruptPure;

        /// <summary>-100 = chaotic, +100 = lawful.</summary>
        public sbyte ChaoticLawful;

        /// <summary>-100 = evil, +100 = good.</summary>
        public sbyte EvilGood;

        /// <summary>-100 = might, +100 = magic.</summary>
        public sbyte MightMagic;

        /// <summary>-100 = vengeful, +100 = forgiving.</summary>
        public sbyte VengefulForgiving;

        /// <summary>-100 = craven, +100 = bold.</summary>
        public sbyte CravenBold;

        /// <summary>0-100. How likely the entity is to take initiative and pursue goals.</summary>
        public byte Initiative;

        /// <summary>0-100. Intensity of goal pursuit and ambition.</summary>
        public byte Ambition;

        /// <summary>0-1. Desire for status and recognition.</summary>
        public float DesireStatus;

        /// <summary>0-1. Desire for wealth and material resources.</summary>
        public float DesireWealth;

        /// <summary>0-1. Desire for power and influence.</summary>
        public float DesirePower;

        /// <summary>0-1. Desire for knowledge and understanding.</summary>
        public float DesireKnowledge;
    }

    /// <summary>
    /// Marker component indicating a goal was completed.
    /// Games add these to GoalCompleted buffer when they detect completion.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct GoalCompleted : IBufferElementData
    {
        /// <summary>Index into MotivationSlot buffer of the completed goal.</summary>
        public byte SlotIndex;

        /// <summary>Spec id of the completed goal (for convenience).</summary>
        public short SpecId;
    }
}

