using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Perception;

namespace PureDOTS.Runtime.Communication
{
    public enum CommunicationMethod : byte
    {
        NativeLanguage = 0,
        KnownLanguage = 1,
        GeneralSigns = 2,
        Empathy = 3,
        Telepathy = 4,
        FailedCommunication = 5
    }

    public enum CommunicationIntent : byte
    {
        Greeting = 0,
        Farewell = 1,
        Gratitude = 2,
        Apology = 3,
        Threat = 4,
        Submission = 5,
        WillingToTrade = 10,
        UnwillingToTrade = 11,
        TradeOfferSpecific = 12,
        TradeRequestSpecific = 13,
        PriceNegotiation = 14,
        AskForDirections = 20,
        ProvideDirections = 21,
        AskForKnowledge = 22,
        ShareKnowledge = 23,
        Warning = 24,
        Rumor = 25,
        PeacefulIntent = 30,
        HostileIntent = 31,
        NeutralIntent = 32,
        HiddenIntent = 33,
        RequestHelp = 40,
        OfferHelp = 41,
        RequestAlliance = 42,
        DeclineRequest = 43,
        SpellIncantation = 50,
        SpellSign = 51,
        TeachSpell = 52,
        Incomprehensible = 255
    }

    public enum CommOrderVerb : byte
    {
        None = 0,
        Flank = 1,
        Spearhead = 2,
        DrawFire = 3,
        Hold = 4,
        Retreat = 5,
        Suppress = 6,
        FocusFire = 7,
        Screen = 8,
        Regroup = 9,
        MoveTo = 10,
        Attack = 11,
        Defend = 12,
        Patrol = 13
    }

    public enum CommOrderSide : byte
    {
        None = 0,
        Left = 1,
        Right = 2,
        Center = 3,
        Front = 4,
        Rear = 5
    }

    public enum CommOrderPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public enum CommMessageType : byte
    {
        Order = 0,
        Ack = 1,
        Nack = 2,
        ClarifyRequest = 3,
        ClarifyResponse = 4
    }

    public enum CommAckPolicy : byte
    {
        None = 0,
        OnHighRisk = 1,
        Required = 2
    }

    [System.Flags]
    public enum CommClarifyQuestionMask : ushort
    {
        None = 0,
        MissingTarget = 1 << 0,
        MissingPosition = 1 << 1,
        MissingSide = 1 << 2,
        MissingTiming = 1 << 3,
        MissingPriority = 1 << 4,
        AmbiguousVerb = 1 << 5,
        UnknownContext = 1 << 6,
        MissingAmount = 1 << 7
    }

    public enum CommNackReason : byte
    {
        None = 0,
        Unclear = 1,
        Unsafe = 2,
        NoAuthority = 3,
        Blocked = 4
    }

    public enum CommDecisionType : byte
    {
        Accepted = 0,
        ClarifyRequested = 1,
        DefaultSafe = 2,
        Ignored = 3
    }

    /// <summary>
    /// Marker + tuning for entities that can send/receive communications.
    /// </summary>
    public struct CommEndpoint : IComponentData
    {
        public PerceptionChannel SupportedChannels;
        public float BaseClarity;
        public float NoiseFloor;

        public static CommEndpoint Default => new CommEndpoint
        {
            SupportedChannels = PerceptionChannel.Hearing | PerceptionChannel.Vision | PerceptionChannel.EM,
            BaseClarity = 1f,
            NoiseFloor = 0f
        };
    }

    [InternalBufferCapacity(2)]
    public struct CommSendRequest : IBufferElementData
    {
        public Entity Receiver;
        public CommMessageType MessageType;
        public uint MessageId;
        public uint RelatedMessageId;
        public CommunicationIntent TrueIntent;
        public CommunicationIntent StatedIntent;
        public FixedString64Bytes PayloadId;
        public PerceptionChannel TransportMask;
        public float DeceptionStrength;
        public uint Timestamp;
        public CommAckPolicy AckPolicy;
        public byte RedundancyLevel;
        public CommClarifyQuestionMask ClarifyMask;
        public CommNackReason NackReason;
        public CommOrderVerb OrderVerb;
        public Entity OrderTarget;
        public float3 OrderTargetPosition;
        public CommOrderSide OrderSide;
        public CommOrderPriority OrderPriority;
        public uint TimingWindowTicks;
        public uint ContextHash;
    }

    [InternalBufferCapacity(2)]
    public struct CommAttempt : IBufferElementData
    {
        public Entity Sender;
        public Entity Receiver;
        public PerceptionChannel TransportMask;
        public CommunicationMethod Method;
        public CommunicationIntent Intent;
        public CommMessageType MessageType;
        public uint MessageId;
        public uint RelatedMessageId;
        public FixedString64Bytes PayloadId;
        public float Clarity;
        public float DeceptionStrength;
        public uint Timestamp;
        public CommAckPolicy AckPolicy;
        public byte RedundancyLevel;
        public CommClarifyQuestionMask ClarifyMask;
        public CommNackReason NackReason;
        public CommOrderVerb OrderVerb;
        public Entity OrderTarget;
        public float3 OrderTargetPosition;
        public CommOrderSide OrderSide;
        public CommOrderPriority OrderPriority;
        public uint TimingWindowTicks;
        public uint ContextHash;
    }

    [InternalBufferCapacity(2)]
    public struct CommReceipt : IBufferElementData
    {
        public Entity Sender;
        public PerceptionChannel Channel;
        public CommunicationMethod Method;
        public CommunicationIntent Intent;
        public CommMessageType MessageType;
        public uint MessageId;
        public uint RelatedMessageId;
        public FixedString64Bytes PayloadId;
        public float Integrity;
        public byte WasDeceptive;
        public uint Timestamp;
        public CommAckPolicy AckPolicy;
        public byte RedundancyLevel;
        public CommClarifyQuestionMask ClarifyMask;
        public CommNackReason NackReason;
        public CommOrderVerb OrderVerb;
        public Entity OrderTarget;
        public float3 OrderTargetPosition;
        public CommOrderSide OrderSide;
        public CommOrderPriority OrderPriority;
        public uint TimingWindowTicks;
        public uint ContextHash;
    }

    [InternalBufferCapacity(2)]
    public struct CommOutboundEntry : IBufferElementData
    {
        public Entity Receiver;
        public CommMessageType MessageType;
        public uint MessageId;
        public uint RelatedMessageId;
        public CommunicationIntent Intent;
        public FixedString64Bytes PayloadId;
        public CommAckPolicy AckPolicy;
        public byte RedundancyLevel;
        public byte RetriesLeft;
        public uint LastSentTick;
        public uint TimeoutTick;
        public CommClarifyQuestionMask ClarifyMask;
        public CommNackReason NackReason;
        public CommOrderVerb OrderVerb;
        public Entity OrderTarget;
        public float3 OrderTargetPosition;
        public CommOrderSide OrderSide;
        public CommOrderPriority OrderPriority;
        public uint TimingWindowTicks;
        public uint ContextHash;
    }

    [InternalBufferCapacity(8)]
    public struct CommRecentMessage : IBufferElementData
    {
        public uint MessageId;
        public uint ReceivedTick;
        public CommMessageType MessageType;
    }

    [InternalBufferCapacity(4)]
    public struct CommPendingClarify : IBufferElementData
    {
        public uint MessageId;
        public uint AskedTick;
        public byte Attempts;
    }

    [InternalBufferCapacity(2)]
    public struct CommDecision : IBufferElementData
    {
        public CommDecisionType Type;
        public uint MessageId;
        public Entity Sender;
        public CommOrderVerb OrderVerb;
        public Entity OrderTarget;
        public float3 OrderTargetPosition;
        public CommOrderSide OrderSide;
        public CommOrderPriority OrderPriority;
        public uint TimingWindowTicks;
        public uint ContextHash;
        public float Confidence;
        public byte Inferred;
        public CommClarifyQuestionMask ClarifyMask;
    }

    public struct CommDecisionConfig : IComponentData
    {
        public float ActThresholdLowRisk;
        public float ActThresholdHighRisk;
        public float ClarifyThreshold;
        public float DefaultSafeRiskCutoff;
        public float MinBacklinkIntegrity;
        public float InferThresholdLowWisdom;
        public float InferThresholdHighWisdom;
        public float MinIntelligenceForInference;
        public byte ClarifyBudgetPerTick;
        public byte ClarifyPerMessageMax;
        public uint AckTimeoutTicks;
        public byte MaxRetries;
        public byte DuplicateHistorySize;

        public static CommDecisionConfig Default => new CommDecisionConfig
        {
            ActThresholdLowRisk = 0.35f,
            ActThresholdHighRisk = 0.75f,
            ClarifyThreshold = 0.25f,
            DefaultSafeRiskCutoff = 0.35f,
            MinBacklinkIntegrity = 0.15f,
            InferThresholdLowWisdom = 0.45f,
            InferThresholdHighWisdom = 0.65f,
            MinIntelligenceForInference = 0.6f,
            ClarifyBudgetPerTick = 1,
            ClarifyPerMessageMax = 1,
            AckTimeoutTicks = 30,
            MaxRetries = 1,
            DuplicateHistorySize = 16
        };
    }

    public struct CommBudgetState : IComponentData
    {
        public uint LastTick;
        public byte ClarifyUsed;
    }

    public struct CommDecodeFactors : IComponentData
    {
        public float Cohesion;
        public float ProtocolFamiliarity;
        public float ContextFit;
        public float Intelligence;
        public float Wisdom;

        public static CommDecodeFactors Default => new CommDecodeFactors
        {
            Cohesion = 1f,
            ProtocolFamiliarity = 1f,
            ContextFit = 1f,
            Intelligence = 0.5f,
            Wisdom = 0.5f
        };
    }
}
