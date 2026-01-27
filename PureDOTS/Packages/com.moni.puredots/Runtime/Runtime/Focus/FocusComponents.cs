using Unity.Entities;

namespace PureDOTS.Runtime.Focus
{
    /// <summary>
    /// Focus resource pool for an entity.
    /// Focus enables temporary boosts to activity effectiveness.
    /// Games configure max focus, regen rate via this component.
    /// </summary>
    public struct EntityFocus : IComponentData
    {
        /// <summary>
        /// Current focus amount (0 to MaxFocus).
        /// </summary>
        public float CurrentFocus;

        /// <summary>
        /// Maximum focus capacity (influenced by Will stat in games).
        /// </summary>
        public float MaxFocus;

        /// <summary>
        /// Base regeneration rate per second when not using abilities.
        /// </summary>
        public float BaseRegenRate;

        /// <summary>
        /// Total drain rate from all active abilities (calculated by FocusAbilitySystem).
        /// </summary>
        public float TotalDrainRate;

        /// <summary>
        /// Exhaustion level (0-100). Increases when focus is depleted while abilities active.
        /// </summary>
        public byte ExhaustionLevel;

        /// <summary>
        /// Whether entity is in a coma state from exhaustion.
        /// </summary>
        public bool IsInComa;

        /// <summary>
        /// Primary archetype affinity (affects ability costs/effectiveness).
        /// </summary>
        public FocusArchetype PrimaryArchetype;

        /// <summary>
        /// Tick when focus was last updated (for determinism).
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Focus archetype categories.
    /// Determines which abilities are available and their effectiveness.
    /// </summary>
    public enum FocusArchetype : byte
    {
        None = 0,

        // Combat archetypes (1-9)
        Finesse = 1,    // Precision, dodging, critical hits
        Physique = 2,   // Raw power, endurance, pain tolerance
        Arcane = 3,     // Magic, summoning, mana manipulation

        // Profession archetypes (10-19)
        Crafting = 10,  // Item creation quality/speed
        Gathering = 11, // Resource harvesting efficiency
        Healing = 12,   // Medical/restoration effectiveness
        Teaching = 13,  // Knowledge transfer speed/depth
        Refining = 14   // Material processing purity/speed
    }

    /// <summary>
    /// Focus ability types - 60+ abilities across all archetypes.
    /// Games can extend with custom abilities in higher ranges.
    /// </summary>
    public enum FocusAbilityType : byte
    {
        None = 0,

        // === Combat: Finesse (10-29) ===
        Parry = 10,             // Increased parry chance
        DualWieldStrike = 11,   // Attack with both weapons
        CriticalFocus = 12,     // Increased critical hit chance
        DodgeBoost = 13,        // Increased dodge chance
        Riposte = 14,           // Counter-attack on successful parry
        PrecisionStrike = 15,   // Bypass armor partially
        Feint = 16,             // Lower enemy dodge for next attack
        QuickDraw = 17,         // Faster weapon switch
        BlindingSpeed = 18,     // Greatly increased attack speed, high drain

        // === Combat: Physique (30-49) ===
        IgnorePain = 30,        // Reduced damage taken
        SweepAttack = 31,       // Hit multiple targets
        AttackSpeedBoost = 32,  // Increased attack speed
        PowerStrike = 33,       // Increased damage
        Charge = 34,            // Rush at enemy with bonus damage
        Intimidate = 35,        // Reduce enemy morale
        SecondWind = 36,        // Recover health over time
        Berserk = 37,           // Max damage, reduced defense
        IronWill = 38,          // Resist stun/knockback

        // === Combat: Arcane (50-69) ===
        SummonBoost = 50,       // Stronger summoned creatures
        ManaRegen = 51,         // Faster mana regeneration
        SpellAmplify = 52,      // Increased spell damage
        Multicast = 53,         // Chance to cast spell twice
        SpellShield = 54,       // Reduced magic damage taken
        Channeling = 55,        // Maintain concentration spells longer
        ArcaneReserve = 56,     // Overchannel mana at health cost
        ElementalMastery = 57,  // Bonus to elemental spells
        Dispel = 58,            // Remove enemy buffs

        // === Crafting (70-89) ===
        MassProduction = 70,    // 2x quantity, reduced quality
        MasterworkFocus = 71,   // +50% quality, 2x time
        BatchCrafting = 72,     // Process multiple items at once
        MaterialSaver = 73,     // Reduced material consumption
        QualityControl = 74,    // Consistent quality, no variance
        ExpertFinish = 75,      // Chance for bonus quality
        RapidAssembly = 76,     // Faster crafting, slight quality loss
        InnovativeCraft = 77,   // Chance for unique properties

        // === Gathering (90-109) ===
        SpeedGather = 90,       // Faster harvesting
        EfficientGather = 91,   // More resources per node
        GatherOverdrive = 92,   // Max speed, high drain
        CarefulExtract = 93,    // Preserve rare materials
        BonusYield = 94,        // Chance for extra resources
        PreserveNode = 95,      // Node lasts longer
        MultiGather = 96,       // Gather from multiple nodes

        // === Healing (110-129) ===
        MassHeal = 110,         // Heal multiple targets, reduced per-target
        LifeClutch = 111,       // Emergency heal at exhaustion cost
        IntensiveCare = 114,    // Strong single-target heal
        Stabilize = 115,        // Prevent death, buy time
        Purify = 116,           // Remove debuffs/poison
        Regenerate = 117,       // Heal over time effect
        SurgicalPrecision = 118,// Bonus to medical procedures

        // === Teaching (130-149) ===
        IntensiveLessons = 130, // Faster learning, high drain
        DeepTeaching = 131,     // Better retention, slower
        GroupInstruction = 132, // Teach multiple students
        MentoringBond = 133,    // Bonus XP to specific student
        PracticalTraining = 134,    // Skill-based teaching bonus
        InspiredTeaching = 135, // Chance for eureka moment

        // === Refining (150-169) ===
        RapidRefine = 150,      // Fast processing, some waste
        PureExtraction = 151,   // Maximum purity, slow
        BatchRefine = 152,      // Process multiple batches
        CatalystBoost = 153,    // Improved catalyst efficiency
        WasteRecovery = 154,    // Recover some waste products
        PrecisionRefine = 155   // Consistent output quality
    }

    /// <summary>
    /// Active focus ability buffer element.
    /// Tracks currently active abilities and their state.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ActiveFocusAbility : IBufferElementData
    {
        /// <summary>
        /// The ability type that is active.
        /// </summary>
        public FocusAbilityType AbilityType;

        /// <summary>
        /// Tick when ability was activated.
        /// </summary>
        public uint ActivatedTick;

        /// <summary>
        /// Duration remaining (0 = indefinite until deactivated).
        /// </summary>
        public float DurationRemaining;

        /// <summary>
        /// Current drain rate for this ability.
        /// </summary>
        public float DrainRate;

        /// <summary>
        /// Stacks of this ability (some abilities stack).
        /// </summary>
        public byte Stacks;
    }

    /// <summary>
    /// Request to activate or deactivate a focus ability.
    /// Processed by FocusAbilitySystem.
    /// </summary>
    public struct FocusAbilityRequest : IComponentData
    {
        /// <summary>
        /// Ability to activate/deactivate.
        /// </summary>
        public FocusAbilityType RequestedAbility;

        /// <summary>
        /// True to activate, false to deactivate.
        /// </summary>
        public bool Activate;

        /// <summary>
        /// Optional duration override (0 = use default).
        /// </summary>
        public float DurationOverride;
    }

    /// <summary>
    /// Calculated profession focus modifiers.
    /// Updated by ProfessionFocusModifierSystem based on active abilities.
    /// Job systems read these modifiers to apply focus effects.
    /// </summary>
    public struct ProfessionFocusModifiers : IComponentData
    {
        /// <summary>
        /// Multiplier for output quality (1.0 = normal).
        /// </summary>
        public float QualityMultiplier;

        /// <summary>
        /// Multiplier for work speed (1.0 = normal).
        /// </summary>
        public float SpeedMultiplier;

        /// <summary>
        /// Multiplier for material waste (1.0 = normal, lower = less waste).
        /// </summary>
        public float WasteMultiplier;

        /// <summary>
        /// Multiplier for target count (healing, teaching).
        /// </summary>
        public float TargetCountMultiplier;

        /// <summary>
        /// Chance for bonus output (0-1).
        /// </summary>
        public float BonusChance;

        /// <summary>
        /// Quantity multiplier for batch processing.
        /// </summary>
        public float QuantityMultiplier;
    }

    /// <summary>
    /// Combat focus modifiers (cached for performance).
    /// Updated by FocusAbilitySystem when combat abilities change.
    /// </summary>
    public struct CombatFocusModifiers : IComponentData
    {
        /// <summary>
        /// Attack speed multiplier.
        /// </summary>
        public float AttackSpeedMultiplier;

        /// <summary>
        /// Damage multiplier.
        /// </summary>
        public float DamageMultiplier;

        /// <summary>
        /// Dodge bonus (additive to base dodge).
        /// </summary>
        public float DodgeBonus;

        /// <summary>
        /// Critical hit chance bonus (additive).
        /// </summary>
        public float CritBonus;

        /// <summary>
        /// Damage reduction (0-1, percentage reduced).
        /// </summary>
        public float DamageReduction;

        /// <summary>
        /// Parry chance bonus (additive).
        /// </summary>
        public float ParryBonus;
    }

    /// <summary>
    /// Focus exhaustion event buffer.
    /// Emitted when exhaustion thresholds are crossed.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct FocusExhaustionEvent : IBufferElementData
    {
        public Entity AffectedEntity;
        public FocusExhaustionEventType EventType;
        public byte PreviousLevel;
        public byte NewLevel;
        public uint Tick;
    }

    /// <summary>
    /// Types of exhaustion events.
    /// </summary>
    public enum FocusExhaustionEventType : byte
    {
        ExhaustionIncreased = 0,
        ExhaustionDecreased = 1,
        ComaEntered = 2,
        ComaExited = 3,
        BreakdownWarning = 4  // Near coma threshold
    }

    /// <summary>
    /// Focus configuration singleton.
    /// Games set these values to tune focus mechanics.
    /// </summary>
    public struct FocusConfig : IComponentData
    {
        /// <summary>
        /// Exhaustion threshold for entering coma (default 100).
        /// </summary>
        public byte ComaThreshold;

        /// <summary>
        /// Exhaustion threshold for breakdown warning (default 80).
        /// </summary>
        public byte BreakdownWarningThreshold;

        /// <summary>
        /// Exhaustion decay rate per second when resting.
        /// </summary>
        public float ExhaustionDecayRate;

        /// <summary>
        /// Exhaustion gain rate when draining focus below 10%.
        /// </summary>
        public float ExhaustionGainRate;

        /// <summary>
        /// Minimum focus percentage to avoid exhaustion gain.
        /// </summary>
        public float SafeFocusThreshold;

        /// <summary>
        /// Duration of coma in seconds before recovery begins.
        /// </summary>
        public float ComaDuration;
    }
}
