using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Registry.Aggregates
{
    /// <summary>
    /// Aggregate registry entry for summarizing entity groups.
    /// </summary>
    public struct AggregateRegistryEntry : IComponentData
    {
        public FixedString32Bytes GroupId;         // "village_001", "fleet_alpha"
        public FixedString32Bytes GroupType;       // "village", "fleet", "army"
        
        // Population/count
        public int EntityCount;
        public int ActiveCount;
        public int IdleCount;
        
        // Resource totals
        public float TotalFood;
        public float TotalGold;
        public float TotalWood;
        public float TotalStone;
        public float TotalMetal;
        
        // Average stats
        public float AverageHealth;
        public float AverageHappiness;
        public float AverageMorale;
        public float AverageSkillLevel;
        
        // Capability summary
        public int CombatCapable;
        public int WorkerCount;
        public int LeaderCount;
        
        // State
        public uint LastUpdatedTick;
        public bool IsCompressed;                   // Using compressed simulation
        public byte CompressionLevel;               // 0=full, 1=partial, 2=minimal
    }

    /// <summary>
    /// Detailed resource breakdown for aggregates.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct AggregateResourceEntry : IBufferElementData
    {
        public ushort ResourceTypeId;
        public float TotalAmount;
        public float ProductionRate;
        public float ConsumptionRate;
        public float NetChange;
    }

    /// <summary>
    /// Request to compress a group for background simulation.
    /// </summary>
    public struct CompressionRequest : IComponentData
    {
        public Entity GroupEntity;
        public byte TargetCompressionLevel;
        public uint RequestTick;
        public bool Force;                          // Compress even if active
    }

    /// <summary>
    /// Configuration for compression behavior.
    /// </summary>
    public struct CompressionConfig : IComponentData
    {
        public float DistanceThreshold;             // Distance from player to compress
        public uint InactivityThreshold;            // Ticks of inactivity to compress
        public uint UpdateIntervalCompressed;       // How often to update compressed entities
        public byte MaxCompressionLevel;
        public bool AllowAutoCompression;
    }

    /// <summary>
    /// Compressed event log for pseudo-history.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct CompressedEvent : IBufferElementData
    {
        public FixedString32Bytes EventType;       // "birth", "death", "harvest", "battle"
        public uint StartTick;
        public uint EndTick;
        public int Count;                           // How many times event occurred
        public float Magnitude;                     // Total impact
    }

    /// <summary>
    /// Pseudo-history entry for compressed simulation.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct PseudoHistoryEntry : IBufferElementData
    {
        public uint Tick;
        public FixedString32Bytes EventType;
        public float Value;
        public Entity RelatedEntity;
    }

    /// <summary>
    /// Background simulation state.
    /// </summary>
    public struct BackgroundSimState : IComponentData
    {
        public uint LastSimTick;
        public uint SimInterval;                    // Ticks between background updates
        public float TimeAccumulator;               // For partial updates
        public bool IsPaused;
        public byte Priority;                       // 0=low, 1=normal, 2=high
    }

    /// <summary>
    /// Decompression request when player approaches.
    /// </summary>
    public struct DecompressionRequest : IComponentData
    {
        public Entity GroupEntity;
        public uint RequestTick;
        public bool Immediate;                      // Skip gradual decompression
    }

    /// <summary>
    /// Tracks which entities are part of an aggregate.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct AggregateMember : IBufferElementData
    {
        public Entity MemberEntity;
        public FixedString32Bytes Role;            // "worker", "soldier", "leader"
        public bool IsActive;
        public uint LastActiveTick;
    }

    /// <summary>
    /// Aggregate production summary.
    /// </summary>
    public struct AggregateProduction : IComponentData
    {
        public float FoodPerTick;
        public float GoldPerTick;
        public float WoodPerTick;
        public float StonePerTick;
        public float MetalPerTick;
        
        public float FoodConsumptionPerTick;
        public float GoldConsumptionPerTick;
        
        public uint LastCalculatedTick;
    }

    /// <summary>
    /// Aggregate military summary.
    /// </summary>
    public struct AggregateMilitary : IComponentData
    {
        public int TotalSoldiers;
        public int TotalArchers;
        public int TotalCavalry;
        public int TotalShips;
        
        public float TotalAttackPower;
        public float TotalDefensePower;
        public float AverageTraining;
        
        public int WoundedCount;
        public int ReserveCount;
    }

    /// <summary>
    /// View request for aggregate data.
    /// </summary>
    public struct AggregateViewRequest : IComponentData
    {
        public FixedString32Bytes GroupId;
        public bool IncludeResources;
        public bool IncludeMilitary;
        public bool IncludeHistory;
        public uint RequestTick;
    }
}

