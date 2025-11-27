using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Types of historical grievances between species/factions.
    /// </summary>
    public enum GrievanceType : byte
    {
        None = 0,

        // === Species-Level Atrocities ===

        /// <summary>
        /// Species was enslaved by another.
        /// </summary>
        Enslavement = 1,

        /// <summary>
        /// Attempted or successful genocide.
        /// </summary>
        Genocide = 2,

        /// <summary>
        /// Homeworld destruction.
        /// </summary>
        HomeworldDestruction = 3,

        /// <summary>
        /// Forced displacement from territories.
        /// </summary>
        Displacement = 4,

        /// <summary>
        /// Cultural suppression/erasure.
        /// </summary>
        CulturalSuppression = 5,

        /// <summary>
        /// Biological experimentation.
        /// </summary>
        Experimentation = 6,

        // === Faction-Level Betrayals ===

        /// <summary>
        /// Broken alliance/treaty.
        /// </summary>
        TreatyBetrayal = 10,

        /// <summary>
        /// Backstab during joint operation.
        /// </summary>
        MilitaryBetrayal = 11,

        /// <summary>
        /// Economic sabotage/exploitation.
        /// </summary>
        EconomicExploitation = 12,

        /// <summary>
        /// Territory conquered/annexed.
        /// </summary>
        TerritoryConquest = 13,

        /// <summary>
        /// Leader assassinated.
        /// </summary>
        LeaderAssassination = 14,

        /// <summary>
        /// Trade embargo that caused suffering.
        /// </summary>
        TradeWarfare = 15,

        // === Personal/Crew Level ===

        /// <summary>
        /// Crew member killed.
        /// </summary>
        CrewMurder = 20,

        /// <summary>
        /// Harsh treatment/abuse.
        /// </summary>
        Mistreatment = 21,

        /// <summary>
        /// Denied promotion/recognition.
        /// </summary>
        CareerSabotage = 22,

        /// <summary>
        /// Family member harmed.
        /// </summary>
        FamilyHarm = 23,

        /// <summary>
        /// False accusation.
        /// </summary>
        FalseAccusation = 24
    }

    /// <summary>
    /// Severity of a grievance determining intensity and decay.
    /// </summary>
    public enum GrievanceSeverity : byte
    {
        /// <summary>
        /// Minor slight, decays quickly.
        /// </summary>
        Minor = 0,

        /// <summary>
        /// Significant harm, moderate decay.
        /// </summary>
        Moderate = 1,

        /// <summary>
        /// Major atrocity, slow decay.
        /// </summary>
        Severe = 2,

        /// <summary>
        /// Unforgivable act, never fully forgiven.
        /// </summary>
        Eternal = 3
    }

    /// <summary>
    /// Species-level historical grievance.
    /// Inherited by all members of the wronged species.
    /// </summary>
    public struct SpeciesGrudge : IComponentData
    {
        /// <summary>
        /// Species ID that committed the grievance.
        /// </summary>
        public ushort OffendingSpeciesId;

        /// <summary>
        /// Type of historical grievance.
        /// </summary>
        public GrievanceType Type;

        /// <summary>
        /// Severity determining decay and effects.
        /// </summary>
        public GrievanceSeverity Severity;

        /// <summary>
        /// Current intensity [0, 100].
        /// </summary>
        public byte Intensity;

        /// <summary>
        /// Original intensity when grievance occurred.
        /// </summary>
        public byte OriginalIntensity;

        /// <summary>
        /// Tick when grievance occurred.
        /// </summary>
        public uint OriginTick;

        /// <summary>
        /// Number of generations since origin.
        /// </summary>
        public ushort GenerationsSinceOrigin;

        /// <summary>
        /// Whether this is common knowledge.
        /// </summary>
        public byte IsPublicKnowledge;

        /// <summary>
        /// Description for narrative/UI.
        /// </summary>
        public FixedString64Bytes Description;
    }

    /// <summary>
    /// Buffer of species-level grudges for an entity's species.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SpeciesGrudgeEntry : IBufferElementData
    {
        public SpeciesGrudge Grudge;
    }

    /// <summary>
    /// Faction-level grudge from recent betrayals/conflicts.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct FactionGrudge : IBufferElementData
    {
        /// <summary>
        /// Offending faction entity.
        /// </summary>
        public Entity OffendingFaction;

        /// <summary>
        /// Type of grievance.
        /// </summary>
        public GrievanceType Type;

        /// <summary>
        /// Severity level.
        /// </summary>
        public GrievanceSeverity Severity;

        /// <summary>
        /// Current intensity [0, 100].
        /// </summary>
        public byte Intensity;

        /// <summary>
        /// Tick when grievance occurred.
        /// </summary>
        public uint OriginTick;

        /// <summary>
        /// Tick when last renewed (repeated offense).
        /// </summary>
        public uint LastRenewedTick;

        /// <summary>
        /// Whether actively seeking revenge.
        /// </summary>
        public byte SeekingRevenge;

        /// <summary>
        /// Reparations demanded (if any).
        /// </summary>
        public float ReparationsDemanded;

        /// <summary>
        /// Reparations received so far.
        /// </summary>
        public float ReparationsReceived;
    }

    /// <summary>
    /// Personal grudge against another entity.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct PersonalGrudge : IBufferElementData
    {
        /// <summary>
        /// Entity that wronged this one.
        /// </summary>
        public Entity Offender;

        /// <summary>
        /// Type of grievance.
        /// </summary>
        public GrievanceType Type;

        /// <summary>
        /// Current intensity [0, 100].
        /// </summary>
        public byte Intensity;

        /// <summary>
        /// Tick when grievance occurred.
        /// </summary>
        public uint OriginTick;

        /// <summary>
        /// Whether grudge is inherited from family.
        /// </summary>
        public byte IsInherited;

        /// <summary>
        /// Whether actively seeking revenge.
        /// </summary>
        public byte SeekingRevenge;
    }

    /// <summary>
    /// Configuration for grudge behavior.
    /// </summary>
    public struct GrudgeBehavior : IComponentData
    {
        /// <summary>
        /// How easily entity holds grudges [0, 100].
        /// </summary>
        public byte Vengefulness;

        /// <summary>
        /// How quickly grudges fade [0, 100].
        /// </summary>
        public byte Forgiveness;

        /// <summary>
        /// Intensity threshold to seek revenge.
        /// </summary>
        public byte RevengeThreshold;

        /// <summary>
        /// Whether entity will inherit family grudges.
        /// </summary>
        public byte InheritsFamilyGrudges;

        /// <summary>
        /// Whether entity cares about species history.
        /// </summary>
        public byte CaresAboutSpeciesHistory;

        /// <summary>
        /// Whether entity acts on faction grudges.
        /// </summary>
        public byte ActsOnFactionGrudges;

        public static GrudgeBehavior Default() => new GrudgeBehavior
        {
            Vengefulness = 40,
            Forgiveness = 50,
            RevengeThreshold = 60,
            InheritsFamilyGrudges = 1,
            CaresAboutSpeciesHistory = 1,
            ActsOnFactionGrudges = 1
        };

        public static GrudgeBehavior Vengeful() => new GrudgeBehavior
        {
            Vengefulness = 80,
            Forgiveness = 20,
            RevengeThreshold = 40,
            InheritsFamilyGrudges = 1,
            CaresAboutSpeciesHistory = 1,
            ActsOnFactionGrudges = 1
        };

        public static GrudgeBehavior Forgiving() => new GrudgeBehavior
        {
            Vengefulness = 20,
            Forgiveness = 80,
            RevengeThreshold = 80,
            InheritsFamilyGrudges = 0,
            CaresAboutSpeciesHistory = 0,
            ActsOnFactionGrudges = 0
        };

        public static GrudgeBehavior SpeciesPride() => new GrudgeBehavior
        {
            Vengefulness = 60,
            Forgiveness = 30,
            RevengeThreshold = 50,
            InheritsFamilyGrudges = 1,
            CaresAboutSpeciesHistory = 1,
            ActsOnFactionGrudges = 0
        };
    }

    /// <summary>
    /// Modifiers from active grudges.
    /// </summary>
    public struct GrudgeModifiers : IComponentData
    {
        /// <summary>
        /// Combat bonus when fighting grudge targets.
        /// </summary>
        public half CombatBonusVsGrudgeTarget;

        /// <summary>
        /// Trade price penalty with grudge holders.
        /// </summary>
        public half TradePenalty;

        /// <summary>
        /// Diplomacy penalty with grudge holders.
        /// </summary>
        public half DiplomacyPenalty;

        /// <summary>
        /// Cooperation penalty when working with grudge target.
        /// </summary>
        public half CooperationPenalty;

        /// <summary>
        /// Morale penalty when serving grudge target.
        /// </summary>
        public half MoralePenalty;

        /// <summary>
        /// Target priority boost for grudge targets.
        /// </summary>
        public half TargetPriorityBoost;

        public static GrudgeModifiers Default() => new GrudgeModifiers
        {
            CombatBonusVsGrudgeTarget = (half)0f,
            TradePenalty = (half)0f,
            DiplomacyPenalty = (half)0f,
            CooperationPenalty = (half)0f,
            MoralePenalty = (half)0f,
            TargetPriorityBoost = (half)0f
        };
    }

    /// <summary>
    /// Request to add a grievance.
    /// </summary>
    public struct GrievanceRequest : IComponentData
    {
        /// <summary>
        /// Entity that caused the grievance.
        /// </summary>
        public Entity Offender;

        /// <summary>
        /// Type of grievance.
        /// </summary>
        public GrievanceType Type;

        /// <summary>
        /// Severity level.
        /// </summary>
        public GrievanceSeverity Severity;

        /// <summary>
        /// Whether this is a species-level grievance.
        /// </summary>
        public byte IsSpeciesLevel;

        /// <summary>
        /// Whether this is a faction-level grievance.
        /// </summary>
        public byte IsFactionLevel;
    }

    /// <summary>
    /// Request for grievance resolution (reparations, apology).
    /// </summary>
    public struct GrievanceResolutionRequest : IComponentData
    {
        /// <summary>
        /// Grudge target to resolve with.
        /// </summary>
        public Entity Target;

        /// <summary>
        /// Type of resolution.
        /// </summary>
        public GrievanceResolutionType ResolutionType;

        /// <summary>
        /// Value offered (reparations amount, etc.).
        /// </summary>
        public float OfferedValue;
    }

    /// <summary>
    /// Types of grievance resolution.
    /// </summary>
    public enum GrievanceResolutionType : byte
    {
        None = 0,

        /// <summary>
        /// Formal apology.
        /// </summary>
        Apology = 1,

        /// <summary>
        /// Financial reparations.
        /// </summary>
        Reparations = 2,

        /// <summary>
        /// Territory return.
        /// </summary>
        TerritoryReturn = 3,

        /// <summary>
        /// Prisoner release.
        /// </summary>
        PrisonerRelease = 4,

        /// <summary>
        /// War crime tribunal.
        /// </summary>
        WarCrimeTribunal = 5,

        /// <summary>
        /// Cultural restoration support.
        /// </summary>
        CulturalRestoration = 6,

        /// <summary>
        /// Trade concessions.
        /// </summary>
        TradeConcessions = 7
    }

    /// <summary>
    /// Event when a grievance is resolved.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct GrievanceResolutionEvent : IBufferElementData
    {
        /// <summary>
        /// Former offender.
        /// </summary>
        public Entity FormerOffender;

        /// <summary>
        /// Grievance type resolved.
        /// </summary>
        public GrievanceType Type;

        /// <summary>
        /// Resolution method.
        /// </summary>
        public GrievanceResolutionType Resolution;

        /// <summary>
        /// Intensity reduction from resolution.
        /// </summary>
        public byte IntensityReduction;

        /// <summary>
        /// Tick when resolved.
        /// </summary>
        public uint ResolvedTick;
    }

    /// <summary>
    /// Static helpers for grudge calculations.
    /// </summary>
    public static class GrudgeHelpers
    {
        /// <summary>
        /// Gets base intensity for a grievance type.
        /// </summary>
        public static byte GetBaseIntensity(GrievanceType type, GrievanceSeverity severity)
        {
            byte baseValue = type switch
            {
                // Atrocities
                GrievanceType.Genocide => 100,
                GrievanceType.HomeworldDestruction => 95,
                GrievanceType.Enslavement => 90,
                GrievanceType.Experimentation => 85,
                GrievanceType.Displacement => 75,
                GrievanceType.CulturalSuppression => 70,

                // Betrayals
                GrievanceType.MilitaryBetrayal => 80,
                GrievanceType.TreatyBetrayal => 70,
                GrievanceType.LeaderAssassination => 75,
                GrievanceType.TerritoryConquest => 65,
                GrievanceType.EconomicExploitation => 55,
                GrievanceType.TradeWarfare => 45,

                // Personal
                GrievanceType.CrewMurder => 70,
                GrievanceType.FamilyHarm => 65,
                GrievanceType.Mistreatment => 40,
                GrievanceType.CareerSabotage => 35,
                GrievanceType.FalseAccusation => 45,

                _ => 30
            };

            // Severity multiplier
            float mult = severity switch
            {
                GrievanceSeverity.Minor => 0.5f,
                GrievanceSeverity.Moderate => 1f,
                GrievanceSeverity.Severe => 1.3f,
                GrievanceSeverity.Eternal => 1.5f,
                _ => 1f
            };

            return (byte)math.min(100, (int)(baseValue * mult));
        }

        /// <summary>
        /// Gets decay rate per tick for a severity level.
        /// </summary>
        public static float GetDecayRate(GrievanceSeverity severity)
        {
            return severity switch
            {
                GrievanceSeverity.Minor => 0.05f,
                GrievanceSeverity.Moderate => 0.02f,
                GrievanceSeverity.Severe => 0.005f,
                GrievanceSeverity.Eternal => 0.0001f, // Almost never decays
                _ => 0.01f
            };
        }

        /// <summary>
        /// Gets cooperation penalty from grudge intensity.
        /// </summary>
        public static float GetCooperationPenalty(byte intensity)
        {
            return intensity * 0.01f; // 0-100%
        }

        /// <summary>
        /// Gets combat bonus when fighting grudge target.
        /// </summary>
        public static float GetCombatBonus(byte intensity, byte vengefulness)
        {
            float base_ = intensity * 0.002f; // 0-20%
            float vengeMult = 1f + vengefulness * 0.005f;
            return base_ * vengeMult;
        }

        /// <summary>
        /// Gets target priority boost for grudge targets.
        /// </summary>
        public static float GetTargetPriorityBoost(byte intensity)
        {
            return intensity * 0.5f; // 0-50 priority boost
        }

        /// <summary>
        /// Calculates intensity reduction from resolution.
        /// </summary>
        public static byte GetResolutionReduction(
            GrievanceResolutionType resolution,
            GrievanceType grievanceType,
            float offeredValue)
        {
            float baseReduction = resolution switch
            {
                GrievanceResolutionType.Apology => 10f,
                GrievanceResolutionType.Reparations => 20f + offeredValue * 0.01f,
                GrievanceResolutionType.TerritoryReturn => 40f,
                GrievanceResolutionType.PrisonerRelease => 25f,
                GrievanceResolutionType.WarCrimeTribunal => 50f,
                GrievanceResolutionType.CulturalRestoration => 35f,
                GrievanceResolutionType.TradeConcessions => 15f,
                _ => 5f
            };

            // Some grievances are harder to resolve
            float difficulty = grievanceType switch
            {
                GrievanceType.Genocide => 0.3f,
                GrievanceType.Enslavement => 0.4f,
                GrievanceType.HomeworldDestruction => 0.2f,
                _ => 1f
            };

            return (byte)math.min(100, (int)(baseReduction * difficulty));
        }

        /// <summary>
        /// Checks if entity should inherit species grudge.
        /// </summary>
        public static bool ShouldInheritSpeciesGrudge(
            in GrudgeBehavior behavior,
            in SpeciesGrudge grudge)
        {
            if (behavior.CaresAboutSpeciesHistory == 0)
                return false;

            // Eternal grudges always inherited
            if (grudge.Severity == GrievanceSeverity.Eternal)
                return true;

            // Severe grudges inherited if intensity still high
            if (grudge.Severity == GrievanceSeverity.Severe && grudge.Intensity > 30)
                return true;

            // Recent moderate+ grudges inherited
            if (grudge.GenerationsSinceOrigin < 3 && grudge.Intensity > 40)
                return true;

            return false;
        }

        /// <summary>
        /// Calculates inherited intensity reduction per generation.
        /// </summary>
        public static byte GetInheritedIntensity(byte originalIntensity, ushort generations, GrievanceSeverity severity)
        {
            float decay = severity switch
            {
                GrievanceSeverity.Minor => 0.5f,
                GrievanceSeverity.Moderate => 0.3f,
                GrievanceSeverity.Severe => 0.15f,
                GrievanceSeverity.Eternal => 0.05f,
                _ => 0.3f
            };

            float remaining = originalIntensity * math.pow(1f - decay, generations);
            return (byte)math.max(0, (int)remaining);
        }
    }
}

