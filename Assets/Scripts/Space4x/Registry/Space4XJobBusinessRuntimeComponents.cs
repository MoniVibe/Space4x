using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Runtime state for a spawned business instance.
    /// </summary>
    public struct Space4XBusinessState : IComponentData
    {
        public Space4XBusinessKind Kind;
        public Space4XBusinessOwnerKind OwnerKind;
        public Entity Owner;
        public Entity Colony;
        public Entity Facility;
        public FacilityBusinessClass FacilityClass;
        public FixedString64Bytes ActiveJobId;
        public uint LastJobTick;
        public uint NextJobTick;
        public float Credits;
    }

    /// <summary>
    /// Marks a colony that has had businesses spawned.
    /// </summary>
    public struct Space4XBusinessSpawnTag : IComponentData
    {
    }

    /// <summary>
    /// Placeholder owner entity for individual/group-owned businesses.
    /// </summary>
    public struct Space4XBusinessOwner : IComponentData
    {
        public Space4XBusinessOwnerKind Kind;
        public Entity HomeColony;
        public uint CreatedTick;
    }

    /// <summary>
    /// Tracks the latest job assigned to an owner.
    /// </summary>
    public struct Space4XJobRoleAssignment : IComponentData
    {
        public FixedString64Bytes JobId;
        public Entity Business;
        public uint AssignedTick;
        public uint NextEvaluateTick;
    }

    /// <summary>
    /// Marks a business that has had its starter fleet seeded.
    /// </summary>
    public struct Space4XBusinessFleetSeeded : IComponentData
    {
    }
}
