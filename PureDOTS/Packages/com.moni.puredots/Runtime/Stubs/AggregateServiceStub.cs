// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Aggregates
{
    public static class AggregateServiceStub
    {
        public static AggregateHandle CreateAggregate(byte type) => default;

        public static void AddMember(AggregateHandle aggregate, in Entity member, byte role) { }

        public static void RemoveMember(AggregateHandle aggregate, in Entity member) { }
    }
}
