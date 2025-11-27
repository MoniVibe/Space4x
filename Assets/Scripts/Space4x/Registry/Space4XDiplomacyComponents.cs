using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Diplomatic stance between factions.
    /// </summary>
    public enum DiplomaticStance : byte
    {
        War = 0,
        Hostile = 1,
        Unfriendly = 2,
        Neutral = 3,
        Cordial = 4,
        Friendly = 5,
        Allied = 6,
        Vassal = 7,
        Overlord = 8
    }

    /// <summary>
    /// Diplomatic status with another faction.
    /// </summary>
    public struct Space4XDiplomaticStatus : IComponentData
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
        /// Current diplomatic stance.
        /// </summary>
        public DiplomaticStance Stance;

        /// <summary>
        /// Relation score [-100, +100].
        /// </summary>
        public sbyte RelationScore;

        /// <summary>
        /// Trust built over time [-1, +1].
        /// </summary>
        public half Trust;

        /// <summary>
        /// Whether we have an open border agreement.
        /// </summary>
        public byte HasOpenBorders;

        /// <summary>
        /// Whether we share sensor data.
        /// </summary>
        public byte HasSensorSharing;

        /// <summary>
        /// Whether we have a defensive pact.
        /// </summary>
        public byte HasDefensePact;

        /// <summary>
        /// Tick when stance last changed.
        /// </summary>
        public uint StanceChangeTick;
    }

    /// <summary>
    /// Buffer of diplomatic statuses with multiple factions.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DiplomaticStatusEntry : IBufferElementData
    {
        public Space4XDiplomaticStatus Status;
    }

    /// <summary>
    /// Modifier affecting relation score.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct RelationModifier : IBufferElementData
    {
        /// <summary>
        /// Type of modifier.
        /// </summary>
        public RelationModifierType Type;

        /// <summary>
        /// Score change.
        /// </summary>
        public sbyte ScoreChange;

        /// <summary>
        /// Decay per 1000 ticks.
        /// </summary>
        public half DecayRate;

        /// <summary>
        /// Remaining duration (0 = permanent until decay).
        /// </summary>
        public uint RemainingTicks;

        /// <summary>
        /// Source faction ID (if applicable).
        /// </summary>
        public ushort SourceFactionId;

        /// <summary>
        /// Tick when modifier was applied.
        /// </summary>
        public uint AppliedTick;
    }

    /// <summary>
    /// Type of relation modifier.
    /// </summary>
    public enum RelationModifierType : byte
    {
        None = 0,

        // Positive
        TradeBonus = 1,
        GiftReceived = 2,
        TreatyHonored = 3,
        SharedEnemy = 4,
        TechShared = 5,
        CrisisHelp = 6,
        LongPeace = 7,
        OutlookMatch = 8,

        // Negative
        BorderIncursion = 20,
        TreatyBroken = 21,
        WarDeclared = 22,
        AllyAttacked = 23,
        EspionageDetected = 24,
        InsultReceived = 25,
        ClaimContested = 26,
        TradeDisrupted = 27,
        OutlookMismatch = 28
    }

    /// <summary>
    /// Type of treaty.
    /// </summary>
    public enum TreatyType : byte
    {
        None = 0,

        // Basic
        NonAggression = 1,
        OpenBorders = 2,
        TradeAgreement = 3,
        SensorSharing = 4,

        // Alliance
        DefensePact = 10,
        MilitaryAlliance = 11,
        Federation = 12,

        // Economic
        CustomsUnion = 20,
        FreeTradeZone = 21,
        SharedMarket = 22,

        // Research
        ResearchPact = 30,
        TechExchange = 31,

        // Vassal
        Protectorate = 40,
        Tributary = 41,
        Vassal = 42,

        // Special
        Ceasefire = 50,
        Surrender = 51,
        Annexation = 52
    }

    /// <summary>
    /// Active treaty between factions.
    /// </summary>
    public struct Space4XTreaty : IComponentData
    {
        /// <summary>
        /// Treaty type.
        /// </summary>
        public TreatyType Type;

        /// <summary>
        /// First party faction ID.
        /// </summary>
        public ushort PartyAFactionId;

        /// <summary>
        /// Second party faction ID.
        /// </summary>
        public ushort PartyBFactionId;

        /// <summary>
        /// First party entity.
        /// </summary>
        public Entity PartyA;

        /// <summary>
        /// Second party entity.
        /// </summary>
        public Entity PartyB;

        /// <summary>
        /// Tick when treaty was signed.
        /// </summary>
        public uint SignedTick;

        /// <summary>
        /// Tick when treaty expires (0 = indefinite).
        /// </summary>
        public uint ExpirationTick;

        /// <summary>
        /// Minimum duration before can be broken.
        /// </summary>
        public uint MinimumDuration;

        /// <summary>
        /// Violation count by party A.
        /// </summary>
        public byte ViolationsA;

        /// <summary>
        /// Violation count by party B.
        /// </summary>
        public byte ViolationsB;

        /// <summary>
        /// Whether treaty is active.
        /// </summary>
        public byte IsActive;

        /// <summary>
        /// Whether treaty was broken (vs expired).
        /// </summary>
        public byte WasBroken;
    }

    /// <summary>
    /// Treaty terms and conditions.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TreatyTerm : IBufferElementData
    {
        /// <summary>
        /// Term type.
        /// </summary>
        public TreatyTermType Type;

        /// <summary>
        /// Faction required to fulfill term.
        /// </summary>
        public ushort ObligatedFactionId;

        /// <summary>
        /// Numeric value for the term.
        /// </summary>
        public float Value;

        /// <summary>
        /// Resource type if applicable.
        /// </summary>
        public MarketResourceType ResourceType;

        /// <summary>
        /// Whether term is fulfilled.
        /// </summary>
        public byte IsFulfilled;
    }

    /// <summary>
    /// Type of treaty term.
    /// </summary>
    public enum TreatyTermType : byte
    {
        None = 0,

        // Resource transfers
        PayCredits = 1,
        PayResources = 2,
        PayTribute = 3,

        // Military
        ProvideFleet = 10,
        DefendTerritory = 11,
        JointOperations = 12,

        // Territory
        CedeSystem = 20,
        AllowAccess = 21,
        DemilitarizeZone = 22,

        // Economic
        ReduceTariff = 30,
        SharedMarketAccess = 31,
        ExclusiveTrade = 32,

        // Research
        ShareTech = 40,
        JointResearch = 41
    }

    /// <summary>
    /// Ambassador entity component.
    /// </summary>
    public struct Space4XAmbassador : IComponentData
    {
        /// <summary>
        /// Ambassador's faction ID.
        /// </summary>
        public ushort HomeFactionId;

        /// <summary>
        /// Posted-to faction ID.
        /// </summary>
        public ushort PostedFactionId;

        /// <summary>
        /// Diplomacy skill [0, 1].
        /// </summary>
        public half DiplomacySkill;

        /// <summary>
        /// Espionage skill [0, 1].
        /// </summary>
        public half EspionageSkill;

        /// <summary>
        /// Charm/persuasion [0, 1].
        /// </summary>
        public half Charm;

        /// <summary>
        /// Current mission.
        /// </summary>
        public AmbassadorMission CurrentMission;

        /// <summary>
        /// Mission progress [0, 1].
        /// </summary>
        public half MissionProgress;

        /// <summary>
        /// Target entity for mission.
        /// </summary>
        public Entity MissionTarget;

        /// <summary>
        /// Relations improvement generated.
        /// </summary>
        public float RelationsGenerated;
    }

    /// <summary>
    /// Ambassador mission type.
    /// </summary>
    public enum AmbassadorMission : byte
    {
        None = 0,
        ImproveRelations = 1,
        NegotiateTreaty = 2,
        GatherIntel = 3,
        ProtestAction = 4,
        DeliverUltimatum = 5,
        ProposeAlliance = 6,
        RequestAid = 7,
        Sabotage = 8
    }

    /// <summary>
    /// Diplomatic action proposal.
    /// </summary>
    public struct DiplomaticProposal : IComponentData
    {
        /// <summary>
        /// Proposing faction ID.
        /// </summary>
        public ushort ProposerFactionId;

        /// <summary>
        /// Target faction ID.
        /// </summary>
        public ushort TargetFactionId;

        /// <summary>
        /// Proposal type.
        /// </summary>
        public DiplomaticProposalType Type;

        /// <summary>
        /// Associated treaty type (if applicable).
        /// </summary>
        public TreatyType TreatyType;

        /// <summary>
        /// Offered value/amount.
        /// </summary>
        public float OfferedValue;

        /// <summary>
        /// Requested value/amount.
        /// </summary>
        public float RequestedValue;

        /// <summary>
        /// Tick when proposal was made.
        /// </summary>
        public uint ProposedTick;

        /// <summary>
        /// Tick when proposal expires.
        /// </summary>
        public uint ExpirationTick;

        /// <summary>
        /// Response status.
        /// </summary>
        public ProposalStatus Status;
    }

    /// <summary>
    /// Type of diplomatic proposal.
    /// </summary>
    public enum DiplomaticProposalType : byte
    {
        None = 0,
        Treaty = 1,
        TradeOffer = 2,
        DeclareWar = 3,
        RequestPeace = 4,
        DemandTribute = 5,
        OfferVassalage = 6,
        RequestAlliance = 7,
        ShareIntel = 8,
        JointWar = 9
    }

    /// <summary>
    /// Status of a diplomatic proposal.
    /// </summary>
    public enum ProposalStatus : byte
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        Countered = 3,
        Expired = 4,
        Withdrawn = 5
    }

    /// <summary>
    /// Diplomacy calculation utilities (candidates for PureDOTS).
    /// </summary>
    public static class DiplomacyMath
    {
        /// <summary>
        /// Calculates relation drift toward base stance.
        /// </summary>
        public static sbyte CalculateRelationDrift(sbyte current, DiplomaticStance stance)
        {
            int target = stance switch
            {
                DiplomaticStance.War => -80,
                DiplomaticStance.Hostile => -50,
                DiplomaticStance.Unfriendly => -20,
                DiplomaticStance.Neutral => 0,
                DiplomaticStance.Cordial => 20,
                DiplomaticStance.Friendly => 50,
                DiplomaticStance.Allied => 75,
                DiplomaticStance.Vassal => 30,
                DiplomaticStance.Overlord => 10,
                _ => 0
            };

            if (current < target)
            {
                return (sbyte)math.min(current + 1, target);
            }
            else if (current > target)
            {
                return (sbyte)math.max(current - 1, target);
            }

            return current;
        }

        /// <summary>
        /// Determines appropriate stance based on relation score.
        /// </summary>
        public static DiplomaticStance DetermineStance(sbyte relationScore, DiplomaticStance current)
        {
            // Don't change stance too frequently
            int threshold = 10;

            if (relationScore <= -70 + threshold && current != DiplomaticStance.War)
            {
                return DiplomaticStance.Hostile;
            }
            else if (relationScore <= -40 + threshold && relationScore > -70 - threshold)
            {
                return DiplomaticStance.Unfriendly;
            }
            else if (relationScore <= 20 + threshold && relationScore > -40 - threshold)
            {
                return DiplomaticStance.Neutral;
            }
            else if (relationScore <= 50 + threshold && relationScore > 20 - threshold)
            {
                return DiplomaticStance.Cordial;
            }
            else if (relationScore <= 75 + threshold && relationScore > 50 - threshold)
            {
                return DiplomaticStance.Friendly;
            }
            else if (relationScore > 75 - threshold)
            {
                return DiplomaticStance.Allied;
            }

            return current;
        }

        /// <summary>
        /// Calculates treaty value for AI decision-making.
        /// </summary>
        public static float CalculateTreatyValue(TreatyType type, in Space4XFaction evaluator, sbyte relationScore)
        {
            float baseValue = type switch
            {
                TreatyType.NonAggression => 20f,
                TreatyType.OpenBorders => 15f,
                TreatyType.TradeAgreement => 25f,
                TreatyType.SensorSharing => 10f,
                TreatyType.DefensePact => 40f,
                TreatyType.MilitaryAlliance => 60f,
                TreatyType.ResearchPact => 30f,
                TreatyType.Protectorate => -50f, // Negative for vassal
                TreatyType.Tributary => -30f,
                _ => 0f
            };

            // Modify based on faction outlook
            if ((evaluator.Outlook & FactionOutlook.Militarist) != 0)
            {
                if (type == TreatyType.DefensePact || type == TreatyType.MilitaryAlliance)
                {
                    baseValue *= 1.3f;
                }
            }

            if ((evaluator.Outlook & FactionOutlook.Materialist) != 0)
            {
                if (type == TreatyType.TradeAgreement || type == TreatyType.CustomsUnion)
                {
                    baseValue *= 1.4f;
                }
            }

            // Relation modifier
            baseValue *= (1f + relationScore * 0.01f);

            return baseValue;
        }

        /// <summary>
        /// Calculates trust change from action.
        /// </summary>
        public static float CalculateTrustChange(RelationModifierType action)
        {
            return action switch
            {
                RelationModifierType.TreatyHonored => 0.1f,
                RelationModifierType.TreatyBroken => -0.5f,
                RelationModifierType.GiftReceived => 0.05f,
                RelationModifierType.WarDeclared => -0.8f,
                RelationModifierType.AllyAttacked => -0.6f,
                RelationModifierType.EspionageDetected => -0.3f,
                RelationModifierType.CrisisHelp => 0.2f,
                RelationModifierType.LongPeace => 0.02f,
                _ => 0f
            };
        }

        /// <summary>
        /// Determines if AI should accept treaty proposal.
        /// </summary>
        public static bool ShouldAcceptTreaty(
            TreatyType type,
            in Space4XFaction evaluator,
            sbyte relationScore,
            float trust,
            float offeredValue,
            float requestedValue)
        {
            float treatyValue = CalculateTreatyValue(type, evaluator, relationScore);
            float trustBonus = trust * 20f;
            float dealValue = offeredValue - requestedValue;

            float acceptanceThreshold = -10f; // Slightly biased toward acceptance

            return (treatyValue + trustBonus + dealValue) > acceptanceThreshold;
        }
    }
}

