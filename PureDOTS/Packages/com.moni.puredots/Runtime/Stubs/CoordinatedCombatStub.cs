// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Cooperation
{
    public static class CoordinatedCombatStub
    {
        public static Entity CreateVolley(in Entity commander, float3 targetPosition) => Entity.Null;

        public static void AddVolleyShooter(in Entity volley, in Entity shooter) { }

        public static float GetVolleyPowerMultiplier(in Entity volley) => 1f;

        public static void FireVolley(in Entity volley) { }
    }
}

