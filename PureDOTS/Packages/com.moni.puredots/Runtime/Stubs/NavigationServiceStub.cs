// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Navigation
{
    public static class NavigationServiceStub
    {
        public static Entity RequestPath(ref SystemState state, in Entity requester, in float3 start, in float3 end, byte flags = 0)
        {
            return Entity.Null;
        }

        public static void CancelPath(in Entity ticket) { }
    }
}
