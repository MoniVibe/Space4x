using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Succession
{
    /// <summary>
    /// Type of succession.
    /// </summary>
    public enum SuccessionType : byte
    {
        Primogeniture = 0,    // Eldest child
        Ultimogeniture = 1,   // Youngest child
        Seniority = 2,        // Oldest in family
        Elective = 3,         // Voted by members
        Meritocratic = 4,     // Best qualified
        Designated = 5,       // Specifically chosen
        Random = 6            // Random among heirs
    }

    /// <summary>
    /// Succession rules for an entity.
    /// </summary>
    public struct SuccessionRules : IComponentData
    {
        public SuccessionType Type;
        public byte AllowFemaleHeirs;
        public byte AllowAdoption;
        public byte RequiresBloodline;
        public float MinAge;               // Minimum age to inherit
        public float MinExpertise;         // Minimum expertise tier
        public byte ExpertiseCategory;     // Required expertise type
    }

    /// <summary>
    /// Heir candidate entry.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct HeirCandidate : IBufferElementData
    {
        public Entity CandidateEntity;
        public byte Priority;              // Lower = higher priority
        public float Claim;                // 0-1 strength of claim
        public float Suitability;          // 0-1 how qualified
        public byte IsDesignated;          // Explicitly named heir
        public byte IsBloodline;           // Blood relation
        public uint AddedTick;
    }

    /// <summary>
    /// Legacy that can be inherited.
    /// </summary>
    public struct Legacy : IComponentData
    {
        public Entity OriginatorEntity;    // Who created this legacy
        public FixedString64Bytes LegacyType;
        public float Value;                // Importance/weight
        public float Integrity;            // 0-1 how intact
        public uint CreatedTick;
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Inheritance package when entity dies.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct InheritanceItem : IBufferElementData
    {
        public FixedString32Bytes ItemType; // "asset", "expertise", "title", "grudge"
        public Entity ItemEntity;          // If entity reference
        public float Value;                // Amount/percentage
        public float TransferEfficiency;   // How much transfers (0-1)
        public byte RequiresAcceptance;    // Heir can refuse
    }

    /// <summary>
    /// Pending succession event.
    /// </summary>
    public struct SuccessionEvent : IComponentData
    {
        public Entity DeceasedEntity;
        public Entity SuccessorEntity;
        public SuccessionType TypeUsed;
        public uint OccurredTick;
        public uint ResolvedTick;
        public byte WasContested;
        public byte WasSuccessful;
    }

    /// <summary>
    /// Succession crisis state.
    /// </summary>
    public struct SuccessionCrisis : IComponentData
    {
        public Entity SubjectEntity;       // What's being contested
        public byte ClaimantCount;
        public float Intensity;            // 0-1 severity
        public uint StartTick;
        public uint DeadlineTick;          // Must resolve by
        public byte RequiresVote;
        public byte RequiresCombat;
    }

    /// <summary>
    /// Chronicle entry for legacy tracking.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct ChronicleEntry : IBufferElementData
    {
        public FixedString64Bytes EventType;
        public Entity RelatedEntity;
        public float Significance;         // 0-1 how important
        public uint OccurredTick;
    }
}

