using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Knowledge
{
    /// <summary>
    /// State of knowledge propagation.
    /// </summary>
    public enum DiffusionState : byte
    {
        Unknown = 0,        // Not yet reached
        Queued = 1,         // In transit
        Arriving = 2,       // Almost there
        Adopted = 3,        // Fully available
        Decaying = 4,       // Losing access
        Lost = 5            // No longer available
    }

    /// <summary>
    /// Type of knowledge being diffused.
    /// </summary>
    public enum KnowledgeCategory : byte
    {
        Technology = 0,
        Crafting = 1,
        Religion = 2,
        Culture = 3,
        Military = 4,
        Economic = 5,
        Scientific = 6,
        Medical = 7
    }

    /// <summary>
    /// A piece of knowledge that can spread.
    /// </summary>
    public struct KnowledgeDefinition : IComponentData
    {
        public FixedString64Bytes KnowledgeId;
        public KnowledgeCategory Category;
        public byte Tier;                  // Complexity tier (affects spread speed)
        public float BaseDiffusionRate;    // How fast it spreads naturally
        public byte RequiresInfrastructure; // Min infrastructure to receive
        public uint UnlockedTick;          // When first discovered
    }

    /// <summary>
    /// Tracks diffusion state for a knowledge item at a location.
    /// </summary>
    public struct KnowledgeDiffusionState : IComponentData
    {
        public FixedString64Bytes KnowledgeId;
        public Entity SourceEntity;        // Where it came from
        public Entity LocationEntity;      // Where it's spreading to
        public DiffusionState State;
        public float Progress;             // 0-1 progress to adoption
        public float DecayProgress;        // 0-1 decay toward lost
        public uint QueuedTick;
        public uint AdoptedTick;
        public uint EstimatedArrivalTick;
    }

    /// <summary>
    /// Buffer of knowledge items in transit to a location.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct DiffusionQueue : IBufferElementData
    {
        public FixedString64Bytes KnowledgeId;
        public Entity SourceEntity;
        public float TravelProgress;       // 0-1 how far along
        public float TravelSpeed;          // Units per tick
        public uint QueuedTick;
    }

    /// <summary>
    /// Infrastructure that affects diffusion.
    /// </summary>
    public struct DiffusionInfrastructure : IComponentData
    {
        public byte InfrastructureTier;    // 0-10
        public float RelayQuality;         // 0-1 network quality
        public float ReceptionBonus;       // Bonus to incoming diffusion
        public float TransmissionBonus;    // Bonus to outgoing diffusion
        public byte HasLibrary;            // Archives accelerate adoption
        public byte HasAcademy;            // Training accelerates adoption
    }

    /// <summary>
    /// Connection between two locations for knowledge transfer.
    /// </summary>
    public struct DiffusionLink : IComponentData
    {
        public Entity SourceEntity;
        public Entity TargetEntity;
        public float Distance;             // Travel distance
        public float LinkQuality;          // 0-1 connection quality
        public float ThroughputLimit;      // Max knowledge per tick
        public byte IsActive;              // Currently transferring
        public byte IsDiplomatic;          // Requires positive relations
    }

    /// <summary>
    /// Shared research agreement between entities.
    /// </summary>
    public struct ResearchPact : IComponentData
    {
        public Entity PartnerEntity;
        public float SyncRate;             // How fast queues sync
        public float ShareRatio;           // What % of research shared
        public uint EstablishedTick;
        public uint ExpiresTick;
        public byte IsActive;
    }

    /// <summary>
    /// Knowledge adoption status at a location.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct AdoptedKnowledge : IBufferElementData
    {
        public FixedString64Bytes KnowledgeId;
        public KnowledgeCategory Category;
        public byte Tier;
        public uint AdoptedTick;
        public float MasteryLevel;         // 0-1 how well understood
        public byte CanTransmit;           // Mastered enough to teach
    }
}

