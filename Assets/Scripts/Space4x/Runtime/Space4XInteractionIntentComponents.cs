using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    /// <summary>
    /// Shared interaction verbs used by both AI and player pathways.
    /// </summary>
    public enum Space4XInteractionIntentAction : byte
    {
        None = 0,
        Hail = 1,
        Trade = 2,
        Attack = 3,
        Socialize = 4,
        Dock = 5,
        Equip = 6,
        Produce = 7
    }

    public enum Space4XInteractionIntentSource : byte
    {
        Unknown = 0,
        Player = 1,
        AgentAI = 2,
        Scripted = 3,
        System = 4
    }

    /// <summary>
    /// Tick-local interaction intent stream. Producers append and downstream systems consume.
    /// </summary>
    public struct Space4XInteractionIntentStream : IComponentData
    {
    }

    [InternalBufferCapacity(32)]
    public struct Space4XInteractionIntent : IBufferElementData
    {
        public Space4XInteractionIntentAction Action;
        public Space4XInteractionIntentSource Source;
        public Entity Actor;
        public Entity Target;
        public Entity ContextEntity;
        public ushort ActorFactionId;
        public ushort TargetFactionId;
        public uint Tick;
        public uint CorrelationId;
        public byte Priority;
        public half Confidence;
        public FixedString64Bytes TopicId;
    }

    /// <summary>
    /// Per-actor dedupe cursor for intent producers.
    /// </summary>
    public struct Space4XInteractionIntentCursor : IComponentData
    {
        public uint LastCommTick;
        public uint LastDockingTick;
        public uint LastGateTick;
    }

    /// <summary>
    /// Lightweight profile influences for relation-aware interaction policy.
    /// </summary>
    public struct Space4XInteractionBehaviorProfile : IComponentData
    {
        /// <summary>
        /// Positive means the actor accepts thinner margins for trade.
        /// Negative means stricter pricing requirements.
        /// </summary>
        public half TradeLeniency;

        /// <summary>
        /// Positive increases attack escalation chance from negative relations.
        /// </summary>
        public half AggressionBias;

        /// <summary>
        /// [0,1] volatility multiplier for chaotic behavior.
        /// </summary>
        public half ChaoticBias;

        /// <summary>
        /// [0,1] adherence multiplier for lawful behavior.
        /// </summary>
        public half LawfulBias;

        public static Space4XInteractionBehaviorProfile Default => new Space4XInteractionBehaviorProfile
        {
            TradeLeniency = (half)0f,
            AggressionBias = (half)0f,
            ChaoticBias = (half)0f,
            LawfulBias = (half)0f
        };
    }

    /// <summary>
    /// Shared policy gate for relation-driven interaction behavior.
    /// </summary>
    public struct Space4XInteractionIntentPolicyConfig : IComponentData
    {
        /// <summary>
        /// Minimum relation score required to keep trade intents.
        /// </summary>
        public sbyte MinTradeRelationScore;

        /// <summary>
        /// Minimum relation score required to keep hail intents.
        /// </summary>
        public sbyte MinHailRelationScore;

        /// <summary>
        /// Relation threshold where attack escalation starts.
        /// </summary>
        public sbyte AttackTriggerRelationScore;

        /// <summary>
        /// Relation threshold where attack escalation is severe.
        /// </summary>
        public sbyte AttackSevereRelationScore;

        public half AttackChanceAtTrigger;
        public half AttackChanceAtSevere;
        public half BaseTradePriceOffset;
        public ushort TradeBrokerFeeBps;
        public ushort TradeSalesTaxBps;
        public ushort TradeHostileFeeBps;
        public ushort TradeRelationDiscountBpsAt100;
        public half TradeDesperationOffsetScale;
        public half TradeScarcityOffsetScale;
        public half TradeExtremeNeedOffsetBonus;
        public half MaterialistLeniency;
        public half SpiritualStrictness;
        public half ChaoticPriceVariance;
        public half LawfulOutlookWeight;

        public static Space4XInteractionIntentPolicyConfig Default => new Space4XInteractionIntentPolicyConfig
        {
            MinTradeRelationScore = 20,
            MinHailRelationScore = 0,
            AttackTriggerRelationScore = -20,
            AttackSevereRelationScore = -50,
            AttackChanceAtTrigger = (half)0.1f,
            AttackChanceAtSevere = (half)0.45f,
            BaseTradePriceOffset = (half)0.02f,
            TradeBrokerFeeBps = 100,
            TradeSalesTaxBps = 75,
            TradeHostileFeeBps = 125,
            TradeRelationDiscountBpsAt100 = 80,
            TradeDesperationOffsetScale = (half)0.7f,
            TradeScarcityOffsetScale = (half)0.45f,
            TradeExtremeNeedOffsetBonus = (half)0.25f,
            MaterialistLeniency = (half)0.025f,
            SpiritualStrictness = (half)0.03f,
            ChaoticPriceVariance = (half)0.06f,
            LawfulOutlookWeight = (half)0.5f
        };
    }
}
