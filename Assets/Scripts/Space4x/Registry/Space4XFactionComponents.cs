using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Type of faction entity.
    /// </summary>
    public enum FactionType : byte
    {
        /// <summary>
        /// Organized interstellar government.
        /// </summary>
        Empire = 0,

        /// <summary>
        /// Raiding opportunists.
        /// </summary>
        Pirate = 1,

        /// <summary>
        /// Alien life forms with instinctual behavior.
        /// </summary>
        Fauna = 2,

        /// <summary>
        /// Trade-focused organization.
        /// </summary>
        Guild = 3,

        /// <summary>
        /// Religious or ideological group.
        /// </summary>
        Cult = 4,

        /// <summary>
        /// Corporate entity focused on profit.
        /// </summary>
        Corporation = 5,

        /// <summary>
        /// Independent colony or station.
        /// </summary>
        Independent = 6,

        /// <summary>
        /// Player-controlled faction.
        /// </summary>
        Player = 7
    }

    /// <summary>
    /// Faction outlook affecting behavior priorities.
    /// </summary>
    [System.Flags]
    public enum FactionOutlook : ushort
    {
        None = 0,
        Expansionist = 1 << 0,
        Isolationist = 1 << 1,
        Militarist = 1 << 2,
        Pacifist = 1 << 3,
        Materialist = 1 << 4,
        Spiritualist = 1 << 5,
        Xenophile = 1 << 6,
        Xenophobe = 1 << 7,
        Egalitarian = 1 << 8,
        Authoritarian = 1 << 9,
        Corrupt = 1 << 10,
        Honorable = 1 << 11
    }

    /// <summary>
    /// Core faction profile.
    /// </summary>
    public struct Space4XFaction : IComponentData
    {
        /// <summary>
        /// Faction type.
        /// </summary>
        public FactionType Type;

        /// <summary>
        /// Faction outlook flags.
        /// </summary>
        public FactionOutlook Outlook;

        /// <summary>
        /// Unique faction identifier.
        /// </summary>
        public ushort FactionId;

        /// <summary>
        /// Base aggression level [0, 1].
        /// </summary>
        public half Aggression;

        /// <summary>
        /// Risk tolerance for operations [0, 1].
        /// </summary>
        public half RiskTolerance;

        /// <summary>
        /// Expansion priority [0, 1].
        /// </summary>
        public half ExpansionDrive;

        /// <summary>
        /// Trade/economy priority [0, 1].
        /// </summary>
        public half TradeFocus;

        /// <summary>
        /// Research priority [0, 1].
        /// </summary>
        public half ResearchFocus;

        /// <summary>
        /// Military priority [0, 1].
        /// </summary>
        public half MilitaryFocus;

        public static Space4XFaction Empire(ushort id, FactionOutlook outlook) => new Space4XFaction
        {
            Type = FactionType.Empire,
            Outlook = outlook,
            FactionId = id,
            Aggression = (half)0.4f,
            RiskTolerance = (half)0.5f,
            ExpansionDrive = (half)0.6f,
            TradeFocus = (half)0.5f,
            ResearchFocus = (half)0.5f,
            MilitaryFocus = (half)0.5f
        };

        public static Space4XFaction Pirate(ushort id) => new Space4XFaction
        {
            Type = FactionType.Pirate,
            Outlook = FactionOutlook.Materialist | FactionOutlook.Corrupt,
            FactionId = id,
            Aggression = (half)0.7f,
            RiskTolerance = (half)0.6f,
            ExpansionDrive = (half)0.3f,
            TradeFocus = (half)0.2f,
            ResearchFocus = (half)0.1f,
            MilitaryFocus = (half)0.8f
        };

        public static Space4XFaction Fauna(ushort id) => new Space4XFaction
        {
            Type = FactionType.Fauna,
            Outlook = FactionOutlook.None,
            FactionId = id,
            Aggression = (half)0.3f,
            RiskTolerance = (half)0.4f,
            ExpansionDrive = (half)0.5f,
            TradeFocus = (half)0f,
            ResearchFocus = (half)0f,
            MilitaryFocus = (half)0.3f
        };

        public static Space4XFaction Guild(ushort id) => new Space4XFaction
        {
            Type = FactionType.Guild,
            Outlook = FactionOutlook.Materialist | FactionOutlook.Pacifist,
            FactionId = id,
            Aggression = (half)0.2f,
            RiskTolerance = (half)0.4f,
            ExpansionDrive = (half)0.4f,
            TradeFocus = (half)0.9f,
            ResearchFocus = (half)0.3f,
            MilitaryFocus = (half)0.2f
        };
    }

    /// <summary>
    /// Goal type for faction AI.
    /// </summary>
    public enum FactionGoalType : byte
    {
        None = 0,

        // Expansion
        ColonizeSystem = 1,
        ClaimTerritory = 2,
        BuildOutpost = 3,

        // Military
        DefendTerritory = 10,
        AttackEnemy = 11,
        RaidTarget = 12,
        Patrol = 13,

        // Economy
        SecureTrade = 20,
        EstablishRoute = 21,
        ExploitResource = 22,
        BuildInfrastructure = 23,

        // Diplomacy
        ImproveRelations = 30,
        FormAlliance = 31,
        DeclareWar = 32,
        SeekTreaty = 33,

        // Research
        ResearchTech = 40,
        AcquireTech = 41,

        // Fauna-specific
        Feed = 50,
        Breed = 51,
        Migrate = 52,
        DefendTerritory_Fauna = 53,

        // Pirate-specific
        Plunder = 60,
        Ransom = 61,
        Hideout = 62,
        Recruit = 63
    }

    /// <summary>
    /// A faction goal with priority and progress.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct Space4XFactionGoal : IBufferElementData
    {
        /// <summary>
        /// Goal type.
        /// </summary>
        public FactionGoalType Type;

        /// <summary>
        /// Priority (lower = higher priority).
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Target entity for the goal.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Target location (if not entity-based).
        /// </summary>
        public float3 TargetLocation;

        /// <summary>
        /// Progress toward goal [0, 1].
        /// </summary>
        public half Progress;

        /// <summary>
        /// Resources allocated to goal.
        /// </summary>
        public float ResourcesAllocated;

        /// <summary>
        /// Tick when goal was created.
        /// </summary>
        public uint CreatedTick;

        /// <summary>
        /// Deadline tick (0 = no deadline).
        /// </summary>
        public uint DeadlineTick;
    }

    /// <summary>
    /// Relation between two factions.
    /// </summary>
    public struct FactionRelation : IComponentData
    {
        /// <summary>
        /// Other faction entity.
        /// </summary>
        public Entity OtherFaction;

        /// <summary>
        /// Other faction ID.
        /// </summary>
        public ushort OtherFactionId;

        /// <summary>
        /// Relation score [-100, +100].
        /// </summary>
        public sbyte Score;

        /// <summary>
        /// Trust level [-1, +1].
        /// </summary>
        public half Trust;

        /// <summary>
        /// Fear level [0, 1].
        /// </summary>
        public half Fear;

        /// <summary>
        /// Respect level [-1, +1].
        /// </summary>
        public half Respect;

        /// <summary>
        /// Trade volume (affects relation).
        /// </summary>
        public float TradeVolume;

        /// <summary>
        /// Recent combat interactions.
        /// </summary>
        public uint RecentCombats;

        /// <summary>
        /// Tick of last interaction.
        /// </summary>
        public uint LastInteractionTick;
    }

    /// <summary>
    /// Buffer of relations with other factions.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct FactionRelationEntry : IBufferElementData
    {
        public FactionRelation Relation;
    }

    /// <summary>
    /// Territory control status.
    /// </summary>
    public struct Space4XTerritoryControl : IComponentData
    {
        /// <summary>
        /// Number of controlled star systems.
        /// </summary>
        public ushort ControlledSystems;

        /// <summary>
        /// Number of colonies.
        /// </summary>
        public ushort ColonyCount;

        /// <summary>
        /// Number of outposts.
        /// </summary>
        public ushort OutpostCount;

        /// <summary>
        /// Number of contested sectors.
        /// </summary>
        public ushort ContestedSectors;

        /// <summary>
        /// Total fleet strength.
        /// </summary>
        public float FleetStrength;

        /// <summary>
        /// Economic output.
        /// </summary>
        public float EconomicOutput;

        /// <summary>
        /// Population count.
        /// </summary>
        public uint Population;

        /// <summary>
        /// Territory expansion rate.
        /// </summary>
        public half ExpansionRate;
    }

    /// <summary>
    /// Faction resource stockpile.
    /// </summary>
    public struct FactionResources : IComponentData
    {
        /// <summary>
        /// Credits/currency.
        /// </summary>
        public float Credits;

        /// <summary>
        /// Raw materials.
        /// </summary>
        public float Materials;

        /// <summary>
        /// Energy reserves.
        /// </summary>
        public float Energy;

        /// <summary>
        /// Influence/political capital.
        /// </summary>
        public float Influence;

        /// <summary>
        /// Research points.
        /// </summary>
        public float Research;

        /// <summary>
        /// Income per tick.
        /// </summary>
        public float IncomeRate;

        /// <summary>
        /// Expenses per tick.
        /// </summary>
        public float ExpenseRate;
    }

    /// <summary>
    /// Pirate-specific behavior data.
    /// </summary>
    public struct PirateBehavior : IComponentData
    {
        /// <summary>
        /// Preferred target type.
        /// </summary>
        public PirateTargetPreference TargetPreference;

        /// <summary>
        /// Minimum value for raid target.
        /// </summary>
        public float MinRaidValue;

        /// <summary>
        /// Willingness to negotiate [0, 1].
        /// </summary>
        public half NegotiationWillingness;

        /// <summary>
        /// Honor rating (affects deal-keeping).
        /// </summary>
        public half Honor;

        /// <summary>
        /// Notoriety level (affects aggro).
        /// </summary>
        public float Notoriety;

        /// <summary>
        /// Hideout entity.
        /// </summary>
        public Entity Hideout;
    }

    /// <summary>
    /// Pirate target preference.
    /// </summary>
    public enum PirateTargetPreference : byte
    {
        Opportunistic = 0,    // Nearest/weakest
        HighValue = 1,        // Rich targets
        Vulnerable = 2,       // Poorly defended
        Specific = 3          // Target specific faction
    }

    /// <summary>
    /// Fauna-specific behavior data.
    /// </summary>
    public struct FaunaBehavior : IComponentData
    {
        /// <summary>
        /// Current need priority.
        /// </summary>
        public FaunaNeed CurrentNeed;

        /// <summary>
        /// Hunger level [0, 1].
        /// </summary>
        public half Hunger;

        /// <summary>
        /// Territorial aggression radius.
        /// </summary>
        public float TerritoryRadius;

        /// <summary>
        /// Breeding readiness [0, 1].
        /// </summary>
        public half BreedingReadiness;

        /// <summary>
        /// Migration destination.
        /// </summary>
        public float3 MigrationTarget;

        /// <summary>
        /// Pack/swarm size.
        /// </summary>
        public ushort PackSize;

        /// <summary>
        /// Whether currently aggressive.
        /// </summary>
        public byte IsAggressive;
    }

    /// <summary>
    /// Fauna need priority.
    /// </summary>
    public enum FaunaNeed : byte
    {
        Idle = 0,
        Feed = 1,
        Breed = 2,
        Migrate = 3,
        Defend = 4,
        Flee = 5
    }

    /// <summary>
    /// Tag for faction headquarters/capital.
    /// </summary>
    public struct FactionCapitalTag : IComponentData
    {
        public ushort FactionId;
    }

    /// <summary>
    /// Utility functions for faction AI calculations.
    /// </summary>
    public static class FactionAIUtility
    {
        /// <summary>
        /// Calculates base affinity between two factions based on outlook overlap.
        /// </summary>
        public static float CalculateOutlookAffinity(FactionOutlook a, FactionOutlook b)
        {
            float affinity = 0f;

            // Shared outlooks increase affinity
            if ((a & FactionOutlook.Expansionist) != 0 && (b & FactionOutlook.Expansionist) != 0) affinity += 0.1f;
            if ((a & FactionOutlook.Isolationist) != 0 && (b & FactionOutlook.Isolationist) != 0) affinity += 0.15f;
            if ((a & FactionOutlook.Materialist) != 0 && (b & FactionOutlook.Materialist) != 0) affinity += 0.1f;
            if ((a & FactionOutlook.Spiritualist) != 0 && (b & FactionOutlook.Spiritualist) != 0) affinity += 0.1f;
            if ((a & FactionOutlook.Xenophile) != 0 && (b & FactionOutlook.Xenophile) != 0) affinity += 0.2f;
            if ((a & FactionOutlook.Honorable) != 0 && (b & FactionOutlook.Honorable) != 0) affinity += 0.15f;

            // Opposing outlooks decrease affinity
            if ((a & FactionOutlook.Militarist) != 0 && (b & FactionOutlook.Pacifist) != 0) affinity -= 0.2f;
            if ((a & FactionOutlook.Pacifist) != 0 && (b & FactionOutlook.Militarist) != 0) affinity -= 0.2f;
            if ((a & FactionOutlook.Xenophile) != 0 && (b & FactionOutlook.Xenophobe) != 0) affinity -= 0.3f;
            if ((a & FactionOutlook.Xenophobe) != 0 && (b & FactionOutlook.Xenophile) != 0) affinity -= 0.3f;
            if ((a & FactionOutlook.Egalitarian) != 0 && (b & FactionOutlook.Authoritarian) != 0) affinity -= 0.15f;
            if ((a & FactionOutlook.Honorable) != 0 && (b & FactionOutlook.Corrupt) != 0) affinity -= 0.25f;

            return math.clamp(affinity, -1f, 1f);
        }

        /// <summary>
        /// Prioritizes goals based on faction profile.
        /// </summary>
        public static byte CalculateGoalPriority(FactionGoalType goal, in Space4XFaction faction)
        {
            float basePriority = 50f;

            switch (goal)
            {
                case FactionGoalType.ColonizeSystem:
                case FactionGoalType.ClaimTerritory:
                    basePriority -= (float)faction.ExpansionDrive * 30f;
                    break;

                case FactionGoalType.DefendTerritory:
                case FactionGoalType.AttackEnemy:
                    basePriority -= (float)faction.MilitaryFocus * 30f;
                    break;

                case FactionGoalType.SecureTrade:
                case FactionGoalType.EstablishRoute:
                    basePriority -= (float)faction.TradeFocus * 30f;
                    break;

                case FactionGoalType.ResearchTech:
                    basePriority -= (float)faction.ResearchFocus * 30f;
                    break;

                case FactionGoalType.RaidTarget:
                case FactionGoalType.Plunder:
                    basePriority -= (float)faction.Aggression * 40f;
                    break;
            }

            return (byte)math.clamp(basePriority, 1f, 100f);
        }

        /// <summary>
        /// Determines if faction should pursue aggressive action.
        /// </summary>
        public static bool ShouldActAggressively(in Space4XFaction faction, float relativeStrength, float relationScore)
        {
            float aggressionThreshold = (float)faction.Aggression * 0.5f + relativeStrength * 0.3f - relationScore * 0.002f;
            return aggressionThreshold > 0.5f;
        }

        /// <summary>
        /// Calculates fauna need priority based on state.
        /// </summary>
        public static FaunaNeed DetermineFaunaNeed(in FaunaBehavior behavior, float nearbyThreat)
        {
            if (nearbyThreat > 0.7f)
            {
                return (float)behavior.Hunger > 0.8f ? FaunaNeed.Defend : FaunaNeed.Flee;
            }

            if ((float)behavior.Hunger > 0.6f)
            {
                return FaunaNeed.Feed;
            }

            if ((float)behavior.BreedingReadiness > 0.8f && behavior.PackSize < 10)
            {
                return FaunaNeed.Breed;
            }

            if ((float)behavior.Hunger < 0.3f && nearbyThreat < 0.2f)
            {
                return FaunaNeed.Migrate;
            }

            return FaunaNeed.Idle;
        }
    }
}

