using System;
using PureDOTS.Runtime.Authority;
using Unity.Entities;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Canonical action tokens used for morality/reputation/outlook appraisal.
    /// Kept compact and culture-weighted; presentation should derive visuals elsewhere.
    /// </summary>
    public enum MoralityActionToken : ushort
    {
        Unknown = 0,

        // Governance / social contract
        ObeyOrder = 1,
        DisobeyOrder = 2,
        Betray = 3,

        // Protection / harm
        DefendHome = 10,
        Rescue = 11,
        Execute = 12,
        Torture = 13,

        // Economy / stewardship
        Donate = 20,
        ExploitWorkers = 21,
        Pollute = 22,
        RestoreNature = 23,
        Deforest = 24,

        // Institution building (generic; games can specialize via context)
        Build = 30,
        BuildShelter = 31,
        BuildDefense = 32
    }

    [Flags]
    public enum MoralityIntentFlags : ushort
    {
        None = 0,
        Benevolent = 1 << 0,
        Malicious = 1 << 1,
        Negligent = 1 << 2,
        Coerced = 1 << 3,
        SelfDefense = 1 << 4,
        Retaliation = 1 << 5,
        Sanctioned = 1 << 6,
        Necessity = 1 << 7
    }

    /// <summary>
    /// Singleton tag for the global morality event queue.
    /// </summary>
    public struct MoralityEventQueueTag : IComponentData
    {
    }

    /// <summary>
    /// Lightweight processing state for debugging/proofs/telemetry. This is not a history log.
    /// </summary>
    public struct MoralityEventProcessingState : IComponentData
    {
        public uint LastProcessedTick;
        public int LastProcessedCount;
        public int TotalProcessedCount;
    }

    /// <summary>
    /// Appraisable event emitted by gameplay systems when meaningful actions occur.
    /// Processing is event-driven and rewind-safe (events are tick-stamped and consumed deterministically).
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct MoralityEvent : IBufferElementData
    {
        public uint Tick;
        public Entity Actor;
        public Entity Scope;
        public Entity Target;
        public MoralityActionToken Token;
        public short Magnitude;
        public byte Confidence255;
        public byte Reserved0;
        public MoralityIntentFlags IntentFlags;
        public IssuedByAuthority IssuedByAuthority;
    }
}
