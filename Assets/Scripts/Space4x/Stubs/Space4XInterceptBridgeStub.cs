// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using PureDOTS.Runtime.Interception;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Stubs
{
    public static class Space4XInterceptBridgeStub
    {
        public static void RequestIntercept(EntityManager manager, Entity interceptor, Entity target, float3 lastKnownPos, float3 velocityEstimate)
        {
            InterceptServiceStub.RequestIntercept(manager, interceptor, target, lastKnownPos, velocityEstimate);
        }
    }
}
