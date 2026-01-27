using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Lore
{
    /// <summary>
    /// Type of lore trigger.
    /// </summary>
    public enum LoreTriggerType : byte
    {
        Location = 0,       // Enter/exit location
        Discovery = 1,      // First-time discovery
        Event = 2,          // Game event occurs
        Time = 3,           // Time/season based
        Entity = 4,         // Specific entity interaction
        Proximity = 5,      // Near specific object
        Achievement = 6     // Accomplishment trigger
    }

    /// <summary>
    /// Lore entry that can be triggered.
    /// </summary>
    public struct LoreEntry : IComponentData
    {
        public FixedString128Bytes Text;
        public FixedString32Bytes Category;
        public FixedString32Bytes SpeakerRole; // "captain", "elder", "narrator"
        public LoreTriggerType TriggerType;
        public float Priority;             // Higher = more important
        public uint MinCooldownTicks;      // Min time before replay
        public uint LastTriggeredTick;
        public byte HasBeenSeen;
        public byte IsOneTime;             // Only show once ever
    }

    /// <summary>
    /// Location-based lore trigger zone.
    /// </summary>
    public struct LoreTriggerZone : IComponentData
    {
        public float3 Center;
        public float Radius;
        public byte TriggerOnEnter;
        public byte TriggerOnExit;
        public byte TriggerOnStay;
        public uint StayDurationRequired;  // Ticks to stay before triggering
    }

    /// <summary>
    /// Lore entries attached to a trigger zone.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ZoneLoreEntry : IBufferElementData
    {
        public FixedString128Bytes Text;
        public FixedString32Bytes SpeakerRole;
        public float Weight;               // Selection weight
        public uint LastTriggeredTick;
        public byte IsDiscoveryLore;       // First time only
    }

    /// <summary>
    /// Discovery log for tracked discoveries.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct DiscoveryLogEntry : IBufferElementData
    {
        public FixedString64Bytes DiscoveryId;
        public FixedString32Bytes Category;
        public uint DiscoveredTick;
        public float Significance;         // Importance score
        public byte WasShared;             // Player saw the notification
    }

    /// <summary>
    /// Quote queue for pending delivery.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct PendingQuote : IBufferElementData
    {
        public FixedString128Bytes Text;
        public FixedString32Bytes SpeakerRole;
        public float Priority;
        public uint QueuedTick;
        public uint ExpiresAt;             // Don't show if too old
    }

    /// <summary>
    /// Lore delivery preferences.
    /// </summary>
    public struct LoreDeliverySettings : IComponentData
    {
        public float MinQuoteInterval;     // Min ticks between quotes
        public float MaxQueueSize;         // Max pending quotes
        public byte PrioritizeDiscoveries; // Discoveries skip queue
        public byte AllowDuplicates;       // Same quote can repeat
        public uint LastQuoteDeliveredTick;
    }

    /// <summary>
    /// Contextual filter for lore selection.
    /// </summary>
    public struct LoreContext : IComponentData
    {
        public FixedString32Bytes CurrentRegion;
        public FixedString32Bytes CurrentSeason;
        public byte ThreatLevel;           // 0-10
        public byte MoodLevel;             // 0=sad, 5=neutral, 10=happy
        public byte InCombat;
        public byte InDiscovery;
    }
}

