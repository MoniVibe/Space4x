using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Comms
{
    [System.Flags]
    public enum CommsMessageFlags : byte
    {
        None = 0,
        IsDeceptive = 1 << 0,
        IsEncrypted = 1 << 1,
        IsBroadcast = 1 << 2,
        RequestsAck = 1 << 3
    }

    /// <summary>
    /// Authorable receiver policy (optional).
    /// If absent: entity does not decode comms.
    /// </summary>
    public struct CommsReceiverConfig : IComponentData
    {
        public byte Enabled;

        /// <summary>Transport channels this receiver can decode (typically Hearing/EM/Paranormal).</summary>
        public PerceptionChannel TransportMask;

        /// <summary>Base decode skill [0..1].</summary>
        public float DecodeSkill;

        /// <summary>Noise floor [0..1] applied after decode (higher = ignores weak comms).</summary>
        public float NoiseFloor;

        /// <summary>Chance scale for misread outcomes [0..1].</summary>
        public float MisreadChanceScale;

        /// <summary>Skill for detecting deception [0..1].</summary>
        public float DeceptionDetectSkill;

        /// <summary>Skill for bypassing secrecy/encryption [0..1].</summary>
        public float SecrecyBypassSkill;

        /// <summary>Max inbox items to keep (hard cap).</summary>
        public byte MaxInbox;

        public static CommsReceiverConfig Default => new CommsReceiverConfig
        {
            Enabled = 1,
            TransportMask = PerceptionChannel.Hearing | PerceptionChannel.EM | PerceptionChannel.Paranormal,
            DecodeSkill = 0.65f,
            NoiseFloor = 0.15f,
            MisreadChanceScale = 0.35f,
            DeceptionDetectSkill = 0.35f,
            SecrecyBypassSkill = 0.25f,
            MaxInbox = 4
        };
    }

    [InternalBufferCapacity(4)]
    public struct CommsInboxEntry : IBufferElementData
    {
        public uint ReceivedTick;
        public uint SourceEmittedTick;
        public uint Token;
        public Entity Sender;
        public float3 Origin;
        public InterruptType IntendedInterrupt;
        public InterruptPriority Priority;
        public FixedString32Bytes PayloadId;
        public PerceptionChannel TransportUsed;
        public float Integrity01;
        public MiscommunicationSeverity MisreadSeverity;
        public MiscommunicationType MisreadType;
        public byte WasDeceptionDetected;
        public byte WasProcessed;
        public byte RepeatCount;
    }

    public enum MiscommunicationSeverity : byte
    {
        None = 0,
        Minor = 1,
        Moderate = 2,
        Major = 3,
        Critical = 4,
        Catastrophic = 5
    }

    public enum MiscommunicationType : byte
    {
        None = 0,
        IntentMisread = 1,
        CommandDelayed = 2,
        MessageLost = 3,
        DeceptionUndetected = 4,
        DeceptionFalsePositive = 5,
        ContextMissing = 6
    }

    [InternalBufferCapacity(2)]
    public struct CommsOutboxEntry : IBufferElementData
    {
        /// <summary>
        /// Stable message token. If zero, CommsDeterminism will generate one on first emission
        /// and the value will be retained for repeats/acks.
        /// </summary>
        public uint Token;

        public InterruptType InterruptType;
        public InterruptPriority Priority;
        public FixedString32Bytes PayloadId;
        public PerceptionChannel TransportMaskPreferred;
        public float Strength01;
        public float Clarity01;
        public float DeceptionStrength01;
        public float Secrecy01;
        public uint TtlTicks;
        public Entity IntendedReceiver; // optional (Entity.Null = broadcast)
        public CommsMessageFlags Flags;

        /// <summary>Absolute focus required to emit (and for shared-moment receivers to decode). 0 = free.</summary>
        public float FocusCost;

        /// <summary>Only applies to vision-based "shared moment" comms; 0 = no cohesion gate.</summary>
        public float MinCohesion01;

        /// <summary>Repeat scheduling: if RequestsAck is set, message can repeat until ack or attempts exhausted.</summary>
        public uint RepeatCadenceTicks;
        public byte Attempts;
        public byte MaxAttempts;
        public uint NextEmitTick;
        public uint FirstEmitTick;
    }

    public struct CommsMessageStreamTag : IComponentData { }

    public struct CommsDeliveryDiagnostics : IComponentData
    {
        public uint TargetedConsidered;
        public uint TargetedExpired;
        public uint TargetedWrongTransport;
        public uint TargetedMissingReceiverConfig;
        public uint TargetedReceiverDisabled;
        public uint TargetedMissingInterrupt;
        public uint TargetedMissingSignal;
        public uint TargetedDuplicateEmission;
        public uint TargetedDelivered;
    }

    [InternalBufferCapacity(64)]
    public struct CommsMessage : IBufferElementData
    {
        public uint Token;
        public uint EmittedTick;
        public uint ExpirationTick;
        public int CellId;
        public Entity Sender;
        public float3 Origin;
        public InterruptType InterruptType;
        public InterruptPriority Priority;
        public FixedString32Bytes PayloadId;
        public PerceptionChannel TransportUsed;
        public float Strength01;
        public float Clarity01;
        public float DeceptionStrength01;
        public float Secrecy01;
        public Entity IntendedReceiver;
        public CommsMessageFlags Flags;
    }

    [InternalBufferCapacity(32)]
    public struct CommsMessageSemantic : IBufferElementData
    {
        public uint Token;
        public CommMessageType MessageType;
        public CommunicationIntent TrueIntent;
        public CommunicationIntent StatedIntent;
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
        public Entity IntendedReceiver;
        public uint CreatedTick;
        public uint RelatedMessageId;
        public uint Timestamp;
    }

    public struct CommsSettings : IComponentData
    {
        public int MaxMessagesInStream;
        public uint MaxMessageAgeTicks;

        public static CommsSettings Default => new CommsSettings
        {
            MaxMessagesInStream = 128,
            MaxMessageAgeTicks = 30u
        };
    }

    public static class CommsDeterminism
    {
        public static uint ComputeToken(uint tick, Entity sender, in FixedString32Bytes payloadId, InterruptType interruptType)
        {
            var hashA = (uint)sender.Index;
            var hashB = unchecked((uint)payloadId.GetHashCode());
            var hashC = (uint)interruptType;
            return math.hash(new uint4(tick, hashA, hashB, hashC));
        }
    }
}

