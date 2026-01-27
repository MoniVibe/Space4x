using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Discovery
{
    /// <summary>
    /// Type of discoverable content.
    /// </summary>
    public enum DiscoveryType : byte
    {
        Location = 0,       // Map position/area
        Technology = 1,     // Research unlock
        Resource = 2,       // Resource deposit
        Entity = 3,         // Creature, structure
        Secret = 4,         // Hidden content
        Anomaly = 5,        // Strange phenomenon
        Artifact = 6        // Special item
    }

    /// <summary>
    /// Visibility state for fog of war.
    /// </summary>
    public enum VisibilityState : byte
    {
        Unknown = 0,        // Never seen
        Explored = 1,       // Previously seen
        Visible = 2,        // Currently visible
        Revealed = 3        // Permanently revealed
    }

    /// <summary>
    /// Research tier/era.
    /// </summary>
    public enum ResearchTier : byte
    {
        Primitive = 0,
        Basic = 1,
        Intermediate = 2,
        Advanced = 3,
        Experimental = 4,
        Transcendent = 5
    }

    /// <summary>
    /// Discovery state for an entity.
    /// </summary>
    public struct DiscoveryState : IComponentData
    {
        public int DiscoveredCount;             // Total discoveries
        public int LocationsExplored;           // Map areas seen
        public int TechnologiesUnlocked;        // Tech tree progress
        public int SecretsFound;                // Hidden content
        public float ExplorationRadius;         // Current vision range
        public uint LastDiscoveryTick;
    }

    /// <summary>
    /// Fog of war state for an entity's vision.
    /// </summary>
    public struct FogOfWarState : IComponentData
    {
        public float VisionRange;               // How far can see
        public float MemoryDecay;               // How fast explored fades
        public byte HasNightVision;
        public byte CanSeeStealth;
        public byte IsBlind;                    // Cannot reveal fog
    }

    /// <summary>
    /// Grid-based visibility map.
    /// </summary>
    public struct VisibilityMap : IComponentData
    {
        public float3 WorldMin;
        public float3 WorldMax;
        public float CellSize;
        public int2 Resolution;
        public uint LastUpdateTick;
        public uint Version;
    }

    /// <summary>
    /// Per-cell visibility data.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct VisibilityCell : IBufferElementData
    {
        public VisibilityState State;
        public uint LastSeenTick;
        public byte ObserverCount;              // Entities currently seeing
        public byte WasRevealed;                // Ever permanently revealed
    }

    /// <summary>
    /// Research/knowledge points pool.
    /// </summary>
    public struct KnowledgePool : IComponentData
    {
        public float ResearchPoints;            // Current accumulated
        public float ResearchRate;              // Points per tick
        public float MaxStorage;                // Cap on accumulation
        public ResearchTier CurrentTier;
        public uint LastAccumulationTick;
    }

    /// <summary>
    /// Technology prerequisite.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TechPrerequisite : IBufferElementData
    {
        public FixedString64Bytes TechId;       // Technology identifier
        public FixedString64Bytes RequiredTechId; // Must have this first
        public ResearchTier RequiredTier;       // Minimum tier
        public float ResearchCost;              // Points to unlock
        public byte IsMet;                      // Prerequisite satisfied
    }

    /// <summary>
    /// Technology research state.
    /// </summary>
    public struct TechnologyState : IComponentData
    {
        public FixedString64Bytes TechId;
        public float ResearchProgress;          // 0-1 completion
        public float TotalCost;                 // Points required
        public ResearchTier Tier;
        public uint UnlockedTick;               // 0 if not unlocked
        public byte IsUnlocked;
        public byte IsResearching;              // Currently being researched
    }

    /// <summary>
    /// Technology effects when unlocked.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TechnologyEffect : IBufferElementData
    {
        public FixedString32Bytes EffectId;
        public float Magnitude;
        public byte IsPercentage;               // Additive or multiplicative
    }

    /// <summary>
    /// Exploration memory for remembered locations.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct ExplorationMemory : IBufferElementData
    {
        public float3 Position;
        public FixedString32Bytes LocationId;
        public DiscoveryType Type;
        public uint DiscoveredTick;
        public float Significance;              // Importance 0-1
        public byte IsBookmarked;
        public byte WasVisited;
    }

    /// <summary>
    /// Discoverable entity marker.
    /// </summary>
    public struct Discoverable : IComponentData
    {
        public DiscoveryType Type;
        public float DiscoveryRadius;           // Range to discover
        public float Difficulty;                // How hard to find
        public FixedString64Bytes DiscoveryId;  // Unique identifier
        public byte RequiresLineOfSight;
        public byte IsHidden;                   // Requires special detection
        public byte WasDiscovered;              // Already found
    }

    /// <summary>
    /// Discovery event when something is found.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DiscoveryEvent : IBufferElementData
    {
        public Entity DiscovererEntity;
        public Entity DiscoveredEntity;
        public DiscoveryType Type;
        public float3 Position;
        public uint Tick;
        public byte WasFirstDiscovery;          // Never found before
    }

    /// <summary>
    /// Scout/explorer specialization.
    /// </summary>
    public struct ExplorerStats : IComponentData
    {
        public float BonusVisionRange;
        public float DiscoveryChanceBonus;
        public float MemoryBonus;               // Better recall
        public float StealthDetection;          // Find hidden things
        public byte IsScout;
        public byte IsResearcher;
    }

    /// <summary>
    /// Research queue entry.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ResearchQueueEntry : IBufferElementData
    {
        public FixedString64Bytes TechId;
        public byte Priority;                   // Queue position
        public byte IsPaused;
    }

    /// <summary>
    /// Intel/information about an entity.
    /// </summary>
    public struct IntelState : IComponentData
    {
        public Entity TargetEntity;
        public float IntelLevel;                // 0-1 knowledge about target
        public float IntelDecay;                // How fast intel becomes stale
        public uint LastUpdateTick;
        public byte IsAccurate;                 // Recent enough to trust
        public byte IsComplete;                 // Full information
    }

    /// <summary>
    /// Known information about target.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct IntelEntry : IBufferElementData
    {
        public FixedString32Bytes InfoType;     // "strength", "location", etc.
        public float Value;
        public float Confidence;                // How sure 0-1
        public uint ObservedTick;
    }

    /// <summary>
    /// Discovery registry singleton.
    /// </summary>
    public struct DiscoveryRegistry : IComponentData
    {
        public int TotalDiscoveries;
        public int UniqueLocations;
        public int UnlockedTechnologies;
        public int PendingResearch;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in discovery registry.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct DiscoveryRegistryEntry : IBufferElementData
    {
        public FixedString64Bytes DiscoveryId;
        public DiscoveryType Type;
        public float3 Position;
        public uint DiscoveredTick;
        public Entity DiscovererEntity;
        public byte IsGlobal;                   // Known to all
    }

    /// <summary>
    /// Tech tree configuration.
    /// </summary>
    public struct TechTreeConfig : IComponentData
    {
        public float BaseResearchRate;
        public float TierCostMultiplier;        // Each tier costs more
        public float ParallelResearchPenalty;   // Penalty for multiple
        public byte MaxParallelResearch;
        public byte RequirePrerequisites;
    }
}

