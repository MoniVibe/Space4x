using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Optional owner reference (any entity can own/parent another).
    /// </summary>
    public struct EntityOwner : IComponentData
    {
        public Entity Value;
    }

    /// <summary>
    /// Membership entries allow an entity to belong to multiple aggregates (factions, crews, cults, dynasties, sockets).
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct EntityMembership : IBufferElementData
    {
        public Entity Group;
        public FixedString64Bytes Role;
        public byte Weight;
        public uint SinceTick;
    }

    /// <summary>
    /// Seats advertise assignable slots on a host entity (bridge stations, shrine sockets, turret hardpoints).
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct EntitySeat : IBufferElementData
    {
        public FixedString64Bytes SeatId;
        public Entity Occupant;
        public byte Capacity;
    }

    /// <summary>
    /// Occupants reference the host + seat they currently inhabit.
    /// </summary>
    public struct EntitySeatAssignment : IComponentData
    {
        public Entity Host;
        public FixedString64Bytes SeatId;
    }
}



