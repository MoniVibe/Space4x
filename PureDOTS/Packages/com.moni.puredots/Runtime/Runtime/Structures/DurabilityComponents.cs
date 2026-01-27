using Unity.Entities;

namespace PureDOTS.Runtime.Structures
{
    /// <summary>
    /// Durability state levels.
    /// </summary>
    public enum DurabilityState : byte
    {
        Pristine = 0,      // 100%
        Good = 1,          // 75-99%
        Worn = 2,          // 50-74%
        Damaged = 3,       // 25-49%
        Critical = 4,      // 1-24%
        Destroyed = 5      // 0%
    }

    /// <summary>
    /// Structure durability component.
    /// </summary>
    public struct StructureDurability : IComponentData
    {
        public float CurrentDurability;
        public float MaxDurability;
        public DurabilityState State;
        public float DamagedThreshold;    // % below which penalties apply (default 0.5)
        public float CriticalThreshold;   // % for severe penalties (default 0.25)
        public float EfficiencyPenalty;   // Current penalty (0-1)
        public bool NeedsRepair;
        public uint LastDamageTick;
        public uint LastRepairTick;
    }

    /// <summary>
    /// Configuration for durability system.
    /// </summary>
    public struct DurabilityConfig : IComponentData
    {
        public float DamagedEfficiencyPenalty;    // e.g., 0.25 = -25% at Damaged
        public float CriticalEfficiencyPenalty;   // e.g., 0.5 = -50% at Critical
        public float NaturalDecayRate;            // Per-day decay (0 = no decay)
        public bool AutoQueueRepair;              // Auto-queue when damaged
        public float RepairCostMultiplier;        // Cost multiplier for repairs
        public uint TicksPerDay;                  // For decay calculation
    }

    /// <summary>
    /// Repair request for a structure.
    /// </summary>
    public struct RepairRequest : IComponentData
    {
        public float RepairAmount;
        public Entity RepairerEntity;
        public float ResourceCost;
    }

    /// <summary>
    /// Damage event for a structure.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct DamageHistoryEntry : IBufferElementData
    {
        public float DamageAmount;
        public Entity SourceEntity;
        public DamageSourceType SourceType;
        public uint Tick;
    }

    /// <summary>
    /// Types of damage sources.
    /// </summary>
    public enum DamageSourceType : byte
    {
        Unknown = 0,
        Combat = 1,         // Battle damage
        Siege = 2,          // Siege weapons
        Decay = 3,          // Natural wear
        Disaster = 4,       // Natural disaster
        Sabotage = 5,       // Intentional destruction
        Overuse = 6         // Exceeded capacity
    }

    /// <summary>
    /// Event when structure state changes.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct DurabilityStateChangedEvent : IBufferElementData
    {
        public DurabilityState OldState;
        public DurabilityState NewState;
        public float OldDurability;
        public float NewDurability;
        public uint Tick;
    }

    /// <summary>
    /// Event when structure is destroyed.
    /// </summary>
    public struct StructureDestroyedEvent : IComponentData
    {
        public float DurabilityAtDestruction;
        public DamageSourceType FinalDamageSource;
        public Entity DestroyerEntity;
        public uint Tick;
    }
}

