// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Aggregates
{
    public struct AggregateHandle : IComponentData
    {
        public int Value;
    }

    public struct AggregateArchetypeId : IComponentData
    {
        public int Value;
    }

    public struct AggregateRole : IComponentData
    {
        public byte Value;
    }

    public struct AggregateFormationTicket : IComponentData
    {
        public int TicketId;
        public byte Type; // band/fleet/guild
    }

    public struct AggregateMembershipElement : IBufferElementData
    {
        public Entity Member;
        public byte Role;
    }

    public struct FleetDescriptor : IComponentData
    {
        public int FleetId;
    }

    public struct BandDescriptor : IComponentData
    {
        public int BandId;
    }

    public struct GuildDescriptor : IComponentData
    {
        public int GuildId;
    }
}
