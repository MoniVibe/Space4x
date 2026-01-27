// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    public static class CombatServiceStub
    {
        public static void ScheduleEngagement(in Entity attacker, in Entity defender) { }

        public static void ReportDamage(in Entity target, float amount) { }

        public static float GetThreatRating(in Entity entity) => 0f;
    }
}
