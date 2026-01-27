using Unity.Entities;

namespace PureDOTS.Runtime.AI
{
    [System.Flags]
    public enum AIAckRequestFlags : byte
    {
        None = 0,
        RequestReceipt = 1 << 0,
        RequestCompletion = 1 << 1,
        EmitIssuedAck = 1 << 2
    }

    public enum AIAckStage : byte
    {
        Issued = 0,
        Received = 1,
        Completed = 2,
        Aborted = 3,
        Rejected = 4,
        Deferred = 5
    }

    public enum AIAckReason : byte
    {
        None = 0,
        SuppressedByChaos = 1,
        SuppressedByFocus = 2,
        SuppressedBySleep = 3,
        BudgetExceeded = 4,
        MissingConfig = 5
    }

    /// <summary>
    /// Optional per-agent ack configuration (both requester + emitter).
    /// If absent: agent neither requests nor emits acks.
    /// </summary>
    public struct AIAckConfig : IComponentData
    {
        public byte Enabled;
        public byte WantsReceiptAcks;
        public byte EmitsReceiptAcks;
        public byte EmitsIssuedAcks;

        /// <summary>Focus ratio threshold below which acks are suppressed [0..1].</summary>
        public float MinFocusRatio;

        /// <summary>Rest/sleep pressure threshold above which acks are suppressed [0..1].</summary>
        public float MaxSleepPressure;

        /// <summary>Max chance [0..1] to skip an ack at full chaos.</summary>
        public float ChaosSkipChanceMax;

        public static AIAckConfig Default => new AIAckConfig
        {
            Enabled = 1,
            WantsReceiptAcks = 1,
            EmitsReceiptAcks = 1,
            EmitsIssuedAcks = 0,
            MinFocusRatio = 0.15f,
            MaxSleepPressure = 0.85f,
            ChaosSkipChanceMax = 0.65f
        };
    }

    public struct AIAckStreamTag : IComponentData { }

    /// <summary>
    /// Compact ack event stream for headless/debug tooling.
    /// Bounded emission via UniversalPerformanceBudget.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct AIAckEvent : IBufferElementData
    {
        public uint Tick;
        public uint Token;
        public Entity Agent;
        public Entity TargetEntity;
        public byte ActionIndex;
        public AIAckStage Stage;
        public AIAckReason Reason;
        public byte Flags;
    }
}


