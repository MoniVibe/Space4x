using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Types of companion bonds between entities.
    /// </summary>
    public enum CompanionKind : byte
    {
        Undefined = 0,
        Friend = 1,
        Lover = 2,
        Sibling = 3,
        Mentor = 4,
        Protégé = 5,
        Rival = 6,
        Nemesis = 7,
        ComradeInArms = 8,
        CaptainShipmaster = 9,
        Other = 10
    }

    /// <summary>
    /// State of a companion bond.
    /// </summary>
    public enum CompanionState : byte
    {
        Forming = 0,      // Early bond
        Active = 1,       // Normal
        Strained = 2,     // Conflict, betrayal, distance
        Broken = 3,       // No longer companions (history preserved)
        EndedByDeath = 4  // One or both dead
    }

    /// <summary>
    /// Tag component marking an entity as a companion bond.
    /// </summary>
    public struct CompanionBondTag : IComponentData
    {
    }

    /// <summary>
    /// Core companion bond data between two entities.
    /// Each bond is its own entity for efficient querying.
    /// </summary>
    public struct CompanionBond : IComponentData
    {
        public Entity A;
        public Entity B;
        public CompanionKind Kind;
        public CompanionState State;
        public float Intensity;     // 0..1 (depth of bond)
        public float TrustAB;       // A's trust in B (0..1)
        public float TrustBA;       // B's trust in A (0..1)
        public float Rivalry;       // 0..1 (0 = no rivalry, 1 = pure rivalry)
        public float Obsession;     // 0..1 (how fixated they are on each other)
        public uint FormedTick;      // When bond was created
        public uint LastUpdateTick;  // Last evolution update
    }

    /// <summary>
    /// Optional narrative arc reference for pre-authored companion storylines.
    /// </summary>
    public struct CompanionArcRef : IComponentData
    {
        public int ArcId;  // References narrative arc template
    }

    /// <summary>
    /// Link from individual to companion bond entity.
    /// Buffer on individuals, typically 0-3 entries.
    /// </summary>
    [InternalBufferCapacity(3)]
    public struct CompanionLink : IBufferElementData
    {
        public Entity Bond;  // Entity with CompanionBond
    }
}

