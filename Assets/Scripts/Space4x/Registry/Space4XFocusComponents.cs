using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Space4X-specific focus archetypes for ship crew roles.
    /// Values start at 100 to avoid collision with PureDOTS base archetypes.
    /// </summary>
    public enum Space4XFocusArchetype : byte
    {
        None = 0,

        /// <summary>
        /// Leadership and coordination - Captains, XOs.
        /// </summary>
        Command = 100,

        /// <summary>
        /// Detection and tracking - Sensors Officers.
        /// </summary>
        Sensors = 101,

        /// <summary>
        /// Targeting and firepower - Weapons Officers.
        /// </summary>
        Weapons = 102,

        /// <summary>
        /// Repairs and efficiency - Chief Engineers.
        /// </summary>
        Engineering = 103,

        /// <summary>
        /// Combat maneuvers - Helm Officers.
        /// </summary>
        Tactical = 104,

        /// <summary>
        /// Facility management - Station Crew.
        /// </summary>
        Operations = 105,

        /// <summary>
        /// Medical care - Ship Doctors.
        /// </summary>
        Medical = 106,

        /// <summary>
        /// Communications and diplomacy - Comms Officers.
        /// </summary>
        Communications = 107
    }

    /// <summary>
    /// Focus pool for Space4X entities.
    /// Will be replaced by PureDOTS EntityFocus once available.
    /// </summary>
    public struct Space4XEntityFocus : IComponentData
    {
        /// <summary>
        /// Current focus available [0, MaxFocus].
        /// </summary>
        public float CurrentFocus;

        /// <summary>
        /// Maximum focus capacity.
        /// </summary>
        public float MaxFocus;

        /// <summary>
        /// Base regeneration rate per tick.
        /// </summary>
        public float BaseRegenRate;

        /// <summary>
        /// Current total drain from active abilities.
        /// </summary>
        public float TotalDrainRate;

        /// <summary>
        /// Exhaustion level [0, 100].
        /// </summary>
        public byte ExhaustionLevel;

        /// <summary>
        /// Whether entity is in focus coma.
        /// </summary>
        public byte IsInComa;

        /// <summary>
        /// Tick when focus was last updated.
        /// </summary>
        public uint LastUpdateTick;

        public float Ratio => MaxFocus > 0 ? CurrentFocus / MaxFocus : 0;
        public bool IsExhausted => ExhaustionLevel > 80;
        public bool CanActivateAbility => IsInComa == 0 && ExhaustionLevel < 95;

        public static Space4XEntityFocus Default(float maxFocus = 100f) => new Space4XEntityFocus
        {
            CurrentFocus = maxFocus,
            MaxFocus = maxFocus,
            BaseRegenRate = 0.1f,
            TotalDrainRate = 0f,
            ExhaustionLevel = 0,
            IsInComa = 0
        };
    }

    /// <summary>
    /// Officer role profile determining available focus abilities.
    /// </summary>
    public struct OfficerFocusProfile : IComponentData
    {
        /// <summary>
        /// Primary archetype (reduced drain for these abilities).
        /// </summary>
        public Space4XFocusArchetype PrimaryArchetype;

        /// <summary>
        /// Secondary archetype (normal drain).
        /// </summary>
        public Space4XFocusArchetype SecondaryArchetype;

        /// <summary>
        /// Affinity for primary archetype [0, 1] - reduces drain.
        /// </summary>
        public half ArchetypeAffinity;

        /// <summary>
        /// Experience with focus abilities [0, 1] - improves effectiveness.
        /// </summary>
        public half FocusExperience;

        /// <summary>
        /// Mental resilience [0, 1] - reduces exhaustion gain.
        /// </summary>
        public half MentalResilience;

        /// <summary>
        /// Whether officer is currently on duty.
        /// </summary>
        public byte IsOnDuty;

        public static OfficerFocusProfile Captain() => new OfficerFocusProfile
        {
            PrimaryArchetype = Space4XFocusArchetype.Command,
            SecondaryArchetype = Space4XFocusArchetype.Tactical,
            ArchetypeAffinity = (half)0.3f,
            FocusExperience = (half)0.5f,
            MentalResilience = (half)0.6f,
            IsOnDuty = 1
        };

        public static OfficerFocusProfile SensorsOfficer() => new OfficerFocusProfile
        {
            PrimaryArchetype = Space4XFocusArchetype.Sensors,
            SecondaryArchetype = Space4XFocusArchetype.Communications,
            ArchetypeAffinity = (half)0.4f,
            FocusExperience = (half)0.3f,
            MentalResilience = (half)0.4f,
            IsOnDuty = 1
        };

        public static OfficerFocusProfile WeaponsOfficer() => new OfficerFocusProfile
        {
            PrimaryArchetype = Space4XFocusArchetype.Weapons,
            SecondaryArchetype = Space4XFocusArchetype.Tactical,
            ArchetypeAffinity = (half)0.4f,
            FocusExperience = (half)0.3f,
            MentalResilience = (half)0.5f,
            IsOnDuty = 1
        };

        public static OfficerFocusProfile ChiefEngineer() => new OfficerFocusProfile
        {
            PrimaryArchetype = Space4XFocusArchetype.Engineering,
            SecondaryArchetype = Space4XFocusArchetype.Operations,
            ArchetypeAffinity = (half)0.5f,
            FocusExperience = (half)0.4f,
            MentalResilience = (half)0.5f,
            IsOnDuty = 1
        };

        public static OfficerFocusProfile HelmOfficer() => new OfficerFocusProfile
        {
            PrimaryArchetype = Space4XFocusArchetype.Tactical,
            SecondaryArchetype = Space4XFocusArchetype.Sensors,
            ArchetypeAffinity = (half)0.4f,
            FocusExperience = (half)0.3f,
            MentalResilience = (half)0.4f,
            IsOnDuty = 1
        };

        public static OfficerFocusProfile FacilityWorker() => new OfficerFocusProfile
        {
            PrimaryArchetype = Space4XFocusArchetype.Operations,
            SecondaryArchetype = Space4XFocusArchetype.Engineering,
            ArchetypeAffinity = (half)0.3f,
            FocusExperience = (half)0.2f,
            MentalResilience = (half)0.3f,
            IsOnDuty = 1
        };
    }

    /// <summary>
    /// Calculated focus modifiers for Space4X systems.
    /// Updated by Space4XFocusModifierSystem based on active abilities.
    /// </summary>
    public struct Space4XFocusModifiers : IComponentData
    {
        // === Sensors Modifiers ===

        /// <summary>
        /// Bonus to detection range and cloaked enemy detection.
        /// </summary>
        public half DetectionBonus;

        /// <summary>
        /// Additional targets that can be tracked simultaneously.
        /// </summary>
        public half TrackingCapacityBonus;

        /// <summary>
        /// Multiplier for sensor scan range.
        /// </summary>
        public half ScanRangeMultiplier;

        /// <summary>
        /// Resistance to enemy ECM/jamming.
        /// </summary>
        public half ECMResistance;

        // === Weapons Modifiers ===

        /// <summary>
        /// Weapon cooling efficiency multiplier.
        /// </summary>
        public half CoolingEfficiency;

        /// <summary>
        /// Accuracy bonus from focus.
        /// </summary>
        public half AccuracyBonus;

        /// <summary>
        /// Rate of fire multiplier.
        /// </summary>
        public half RateOfFireMultiplier;

        /// <summary>
        /// Additional missile targets.
        /// </summary>
        public byte MultiTargetCount;

        /// <summary>
        /// Subsystem targeting accuracy bonus.
        /// </summary>
        public half SubsystemTargetingBonus;

        // === Engineering Modifiers ===

        /// <summary>
        /// Repair speed multiplier.
        /// </summary>
        public half RepairSpeedMultiplier;

        /// <summary>
        /// System efficiency bonus.
        /// </summary>
        public half SystemEfficiencyBonus;

        /// <summary>
        /// Damage control effectiveness.
        /// </summary>
        public half DamageControlBonus;

        /// <summary>
        /// Shield adaptation rate.
        /// </summary>
        public half ShieldAdaptationRate;

        // === Tactical Modifiers ===

        /// <summary>
        /// Ship evasion bonus.
        /// </summary>
        public half EvasionBonus;

        /// <summary>
        /// Formation cohesion bonus.
        /// </summary>
        public half FormationCohesionBonus;

        /// <summary>
        /// Strike craft coordination bonus.
        /// </summary>
        public half StrikeCraftCoordinationBonus;

        // === Command Modifiers ===

        /// <summary>
        /// Bonus provided to supported officers.
        /// </summary>
        public half OfficerSupportBonus;

        /// <summary>
        /// Boarding party effectiveness bonus.
        /// </summary>
        public half BoardingEffectivenessBonus;

        /// <summary>
        /// Crew stress reduction.
        /// </summary>
        public half CrewStressReduction;

        /// <summary>
        /// Morale bonus to bridge crew.
        /// </summary>
        public half MoraleBonus;

        // === Operations Modifiers ===

        /// <summary>
        /// Production speed multiplier.
        /// </summary>
        public half ProductionSpeedMultiplier;

        /// <summary>
        /// Production quality multiplier.
        /// </summary>
        public half ProductionQualityMultiplier;

        /// <summary>
        /// Resource efficiency (reduces waste).
        /// </summary>
        public half ResourceEfficiencyBonus;

        /// <summary>
        /// Batch processing capacity bonus.
        /// </summary>
        public byte BatchCapacityBonus;

        public static Space4XFocusModifiers Default() => new Space4XFocusModifiers
        {
            DetectionBonus = (half)0f,
            TrackingCapacityBonus = (half)0f,
            ScanRangeMultiplier = (half)1f,
            ECMResistance = (half)0f,
            CoolingEfficiency = (half)1f,
            AccuracyBonus = (half)0f,
            RateOfFireMultiplier = (half)1f,
            MultiTargetCount = 0,
            SubsystemTargetingBonus = (half)0f,
            RepairSpeedMultiplier = (half)1f,
            SystemEfficiencyBonus = (half)0f,
            DamageControlBonus = (half)0f,
            ShieldAdaptationRate = (half)0f,
            EvasionBonus = (half)0f,
            FormationCohesionBonus = (half)0f,
            StrikeCraftCoordinationBonus = (half)0f,
            OfficerSupportBonus = (half)0f,
            BoardingEffectivenessBonus = (half)0f,
            CrewStressReduction = (half)0f,
            MoraleBonus = (half)0f,
            ProductionSpeedMultiplier = (half)1f,
            ProductionQualityMultiplier = (half)1f,
            ResourceEfficiencyBonus = (half)0f,
            BatchCapacityBonus = 0
        };
    }

    /// <summary>
    /// Active focus ability entry.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct Space4XActiveFocusAbility : IBufferElementData
    {
        /// <summary>
        /// Ability type ID.
        /// </summary>
        public ushort AbilityType;

        /// <summary>
        /// Current drain rate per tick.
        /// </summary>
        public float DrainRate;

        /// <summary>
        /// Tick when ability was activated.
        /// </summary>
        public uint ActivatedTick;

        /// <summary>
        /// Duration remaining (0 = until deactivated).
        /// </summary>
        public uint RemainingDuration;

        /// <summary>
        /// Current effectiveness [0, 1].
        /// </summary>
        public half Effectiveness;

        /// <summary>
        /// Target entity for directed abilities (e.g., OfficerSupport).
        /// </summary>
        public Entity TargetEntity;
    }

    /// <summary>
    /// Request to activate a focus ability.
    /// </summary>
    public struct FocusAbilityRequest : IComponentData
    {
        /// <summary>
        /// Requested ability type.
        /// </summary>
        public ushort RequestedAbility;

        /// <summary>
        /// Optional target for directed abilities.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Requested duration (0 = until deactivated).
        /// </summary>
        public uint RequestedDuration;
    }

    /// <summary>
    /// Request to deactivate a focus ability.
    /// </summary>
    public struct FocusAbilityDeactivateRequest : IComponentData
    {
        /// <summary>
        /// Ability to deactivate.
        /// </summary>
        public ushort AbilityToDeactivate;
    }

    /// <summary>
    /// Captain support link - tracks which officer a captain is supporting.
    /// </summary>
    public struct CaptainSupportLink : IComponentData
    {
        /// <summary>
        /// Currently supported officer entity.
        /// </summary>
        public Entity SupportedOfficer;

        /// <summary>
        /// Support effectiveness bonus.
        /// </summary>
        public half SupportBonus;

        /// <summary>
        /// Tick when support started.
        /// </summary>
        public uint SupportStartTick;
    }

    /// <summary>
    /// Tag for entities receiving captain support.
    /// </summary>
    public struct ReceivingCaptainSupportTag : IComponentData
    {
        /// <summary>
        /// Supporting captain entity.
        /// </summary>
        public Entity SupportingCaptain;

        /// <summary>
        /// Bonus received.
        /// </summary>
        public half BonusReceived;
    }

    /// <summary>
    /// Focus exhaustion event for tracking mental strain.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct FocusExhaustionEvent : IBufferElementData
    {
        /// <summary>
        /// Exhaustion cause.
        /// </summary>
        public FocusExhaustionCause Cause;

        /// <summary>
        /// Exhaustion amount gained.
        /// </summary>
        public byte ExhaustionGained;

        /// <summary>
        /// Tick when event occurred.
        /// </summary>
        public uint Tick;
    }

    /// <summary>
    /// Cause of focus exhaustion.
    /// </summary>
    public enum FocusExhaustionCause : byte
    {
        AbilityDrain = 0,
        EmptyPool = 1,
        OverExertion = 2,
        CombatStress = 3,
        ExtendedDuty = 4,
        Injury = 5
    }

    /// <summary>
    /// Focus coma recovery state.
    /// </summary>
    public struct FocusComaRecovery : IComponentData
    {
        /// <summary>
        /// Tick when coma started.
        /// </summary>
        public uint ComaStartTick;

        /// <summary>
        /// Minimum recovery duration.
        /// </summary>
        public uint MinRecoveryDuration;

        /// <summary>
        /// Recovery progress [0, 1].
        /// </summary>
        public half RecoveryProgress;
    }

    /// <summary>
    /// Personality traits affecting focus usage behavior.
    /// Determines how willingly an entity uses their focus.
    /// </summary>
    public struct FocusPersonality : IComponentData
    {
        /// <summary>
        /// Drive to achieve goals [0, 1]. High = uses focus readily for objectives.
        /// </summary>
        public half Ambition;

        /// <summary>
        /// Tendency to avoid exertion [0, 1]. High = avoids focus use.
        /// </summary>
        public half Laziness;

        /// <summary>
        /// Preference for comfort over achievement [0, 1]. High = conserves focus for leisure.
        /// </summary>
        public half ComfortSeeking;

        /// <summary>
        /// Time spent socializing vs working [0, 1]. High = less focused work time.
        /// </summary>
        public half Sociability;

        /// <summary>
        /// Burning passion driving exceptional effort [0, 1]. Overrides laziness when high.
        /// </summary>
        public half Passion;

        /// <summary>
        /// Self-preservation instinct [0, 1]. Enables full focus use when threatened.
        /// </summary>
        public half SurvivalInstinct;

        /// <summary>
        /// Discipline to push through discomfort [0, 1]. Reduces exhaustion aversion.
        /// </summary>
        public half Discipline;

        /// <summary>
        /// Gets the target focus usage ratio [0, 1] based on personality.
        /// Most entities won't naturally use 100% focus.
        /// </summary>
        public float GetNaturalFocusUsageRatio(bool isThreatened, bool hasActiveGoal)
        {
            float baseUsage = 0.3f; // Baseline minimal effort

            // Ambition increases willingness to use focus
            baseUsage += (float)Ambition * 0.4f;

            // Passion can push beyond normal limits
            if ((float)Passion > 0.7f)
            {
                baseUsage += ((float)Passion - 0.7f) * 0.5f;
            }

            // Laziness reduces focus usage
            baseUsage -= (float)Laziness * 0.3f;

            // Comfort seeking reduces usage
            baseUsage -= (float)ComfortSeeking * 0.2f;

            // Survival instinct enables full usage when threatened
            if (isThreatened)
            {
                float survivalBoost = (float)SurvivalInstinct * 0.6f;
                baseUsage = math.max(baseUsage, 0.5f + survivalBoost);
            }

            // Active goal with discipline pushes usage
            if (hasActiveGoal)
            {
                baseUsage += (float)Discipline * 0.2f;
            }

            return math.saturate(baseUsage);
        }

        /// <summary>
        /// Average, balanced personality.
        /// </summary>
        public static FocusPersonality Average() => new FocusPersonality
        {
            Ambition = (half)0.4f,
            Laziness = (half)0.3f,
            ComfortSeeking = (half)0.4f,
            Sociability = (half)0.5f,
            Passion = (half)0.2f,
            SurvivalInstinct = (half)0.7f,
            Discipline = (half)0.4f
        };

        /// <summary>
        /// Highly driven, ambitious personality.
        /// </summary>
        public static FocusPersonality Ambitious() => new FocusPersonality
        {
            Ambition = (half)0.9f,
            Laziness = (half)0.1f,
            ComfortSeeking = (half)0.2f,
            Sociability = (half)0.3f,
            Passion = (half)0.7f,
            SurvivalInstinct = (half)0.8f,
            Discipline = (half)0.8f
        };

        /// <summary>
        /// Laid-back, comfort-seeking personality.
        /// </summary>
        public static FocusPersonality Relaxed() => new FocusPersonality
        {
            Ambition = (half)0.2f,
            Laziness = (half)0.6f,
            ComfortSeeking = (half)0.7f,
            Sociability = (half)0.7f,
            Passion = (half)0.1f,
            SurvivalInstinct = (half)0.6f,
            Discipline = (half)0.2f
        };

        /// <summary>
        /// Passionate specialist driven by love of craft.
        /// </summary>
        public static FocusPersonality Passionate() => new FocusPersonality
        {
            Ambition = (half)0.5f,
            Laziness = (half)0.2f,
            ComfortSeeking = (half)0.3f,
            Sociability = (half)0.4f,
            Passion = (half)0.95f,
            SurvivalInstinct = (half)0.5f,
            Discipline = (half)0.6f
        };

        /// <summary>
        /// Military-trained disciplined personality.
        /// </summary>
        public static FocusPersonality Disciplined() => new FocusPersonality
        {
            Ambition = (half)0.6f,
            Laziness = (half)0.1f,
            ComfortSeeking = (half)0.2f,
            Sociability = (half)0.3f,
            Passion = (half)0.4f,
            SurvivalInstinct = (half)0.9f,
            Discipline = (half)0.9f
        };
    }

    /// <summary>
    /// Experience and wisdom gained through focus usage.
    /// Entities who use focus more reach greater heights.
    /// </summary>
    public struct FocusGrowth : IComponentData
    {
        /// <summary>
        /// Total focus experience accumulated [0, ∞).
        /// </summary>
        public float TotalFocusExperience;

        /// <summary>
        /// Wisdom gained from sustained focus use [0, 1].
        /// </summary>
        public half Wisdom;

        /// <summary>
        /// Current growth level (derived from experience).
        /// </summary>
        public byte GrowthLevel;

        /// <summary>
        /// Experience towards next level.
        /// </summary>
        public float ExperienceToNextLevel;

        /// <summary>
        /// Cumulative focus time (ticks with abilities active).
        /// </summary>
        public uint CumulativeFocusTime;

        /// <summary>
        /// Peak focus intensity achieved [0, 1].
        /// </summary>
        public half PeakIntensityAchieved;

        /// <summary>
        /// Number of times pushed to exhaustion (builds resilience).
        /// </summary>
        public ushort ExhaustionEvents;

        /// <summary>
        /// Number of breakthrough moments (used 90%+ focus).
        /// </summary>
        public ushort BreakthroughMoments;

        /// <summary>
        /// Gets bonus multiplier from growth level.
        /// </summary>
        public float GetGrowthBonus()
        {
            return 1f + GrowthLevel * 0.05f + (float)Wisdom * 0.2f;
        }

        /// <summary>
        /// Gets experience required for a level.
        /// </summary>
        public static float GetExperienceForLevel(int level)
        {
            return 100f * math.pow(1.5f, level);
        }
    }

    /// <summary>
    /// Tracks focus usage patterns for experience calculation.
    /// </summary>
    public struct FocusUsageTracking : IComponentData
    {
        /// <summary>
        /// Focus drain accumulated this session.
        /// </summary>
        public float SessionDrainAccumulated;

        /// <summary>
        /// Peak drain rate achieved this session.
        /// </summary>
        public float SessionPeakDrainRate;

        /// <summary>
        /// Ticks of continuous focus use.
        /// </summary>
        public uint ContinuousFocusTicks;

        /// <summary>
        /// Whether currently in a "flow state" (sustained high focus).
        /// </summary>
        public byte IsInFlowState;

        /// <summary>
        /// Tick when flow state started.
        /// </summary>
        public uint FlowStateStartTick;

        /// <summary>
        /// Current intensity tier (0=idle, 1=light, 2=moderate, 3=intense, 4=breakthrough).
        /// </summary>
        public byte IntensityTier;

        /// <summary>
        /// Determines intensity tier from drain ratio.
        /// </summary>
        public static byte GetIntensityTier(float drainRatio)
        {
            if (drainRatio < 0.1f) return 0;      // Idle
            if (drainRatio < 0.3f) return 1;      // Light
            if (drainRatio < 0.6f) return 2;      // Moderate
            if (drainRatio < 0.9f) return 3;      // Intense
            return 4;                              // Breakthrough
        }
    }

    /// <summary>
    /// Record of significant focus achievements.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct FocusAchievement : IBufferElementData
    {
        /// <summary>
        /// Achievement type.
        /// </summary>
        public FocusAchievementType Type;

        /// <summary>
        /// Tick when achieved.
        /// </summary>
        public uint AchievedTick;

        /// <summary>
        /// Associated value (e.g., duration, level).
        /// </summary>
        public float Value;
    }

    /// <summary>
    /// Types of focus achievements that grant bonus experience.
    /// </summary>
    public enum FocusAchievementType : byte
    {
        /// <summary>
        /// First time entering flow state.
        /// </summary>
        FirstFlowState = 0,

        /// <summary>
        /// Sustained flow state for 100+ ticks.
        /// </summary>
        SustainedFlow = 1,

        /// <summary>
        /// Reached 90%+ focus usage.
        /// </summary>
        BreakthroughMoment = 2,

        /// <summary>
        /// Recovered from exhaustion without coma.
        /// </summary>
        ExhaustionRecovery = 3,

        /// <summary>
        /// Activated 3+ abilities simultaneously.
        /// </summary>
        MultiAbilityMastery = 4,

        /// <summary>
        /// Reached new growth level.
        /// </summary>
        LevelUp = 5,

        /// <summary>
        /// Used focus in life-threatening situation.
        /// </summary>
        SurvivalFocus = 6,

        /// <summary>
        /// Maintained focus during crisis situation.
        /// </summary>
        CrisisFocus = 7
    }

    /// <summary>
    /// Current goals and threats affecting focus behavior.
    /// </summary>
    public struct FocusBehaviorContext : IComponentData
    {
        /// <summary>
        /// Whether entity perceives a threat.
        /// </summary>
        public byte IsThreatened;

        /// <summary>
        /// Whether entity has an active priority goal.
        /// </summary>
        public byte HasActiveGoal;

        /// <summary>
        /// Current goal importance [0, 1].
        /// </summary>
        public half GoalImportance;

        /// <summary>
        /// Threat severity [0, 1]. 1 = life-threatening.
        /// </summary>
        public half ThreatSeverity;

        /// <summary>
        /// Social pressure to perform [0, 1].
        /// </summary>
        public half SocialPressure;

        /// <summary>
        /// Recent reward received (motivates further effort).
        /// </summary>
        public half RecentReward;

        /// <summary>
        /// Fatigue from non-focus sources (reduces willingness).
        /// </summary>
        public half ExternalFatigue;
    }
}

