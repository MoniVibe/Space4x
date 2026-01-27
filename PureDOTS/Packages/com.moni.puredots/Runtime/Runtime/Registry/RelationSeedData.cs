using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Seed data for initial relations between entities.
    /// Used in scenarios to set up guild/village/fleet membership, allegiance, hostility.
    /// </summary>
    public struct RelationSeed
    {
        public Entity EntityA;
        public Entity EntityB;
        public FixedString32Bytes RelationType;  // "member", "ally", "hostile", "leader", etc.
        public float RelationValue;              // Relation strength (-1 to +1)
    }

    /// <summary>
    /// Buffer of relation seeds to apply after entity spawn.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct RelationSeedElement : IBufferElementData
    {
        public FixedString64Bytes EntityAId;    // Registry ID or entity reference
        public FixedString64Bytes EntityBId;
        public FixedString32Bytes RelationType;
        public float RelationValue;
    }
}



