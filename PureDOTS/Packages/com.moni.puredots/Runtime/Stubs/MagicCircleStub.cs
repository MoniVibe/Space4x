// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Cooperation
{
    public static class MagicCircleStub
    {
        public static Entity CreateMagicCircle(in Entity primaryCaster) => Entity.Null;

        public static void AddCircleMember(in Entity circle, in Entity contributor) { }

        public static float GetPooledMana(in Entity circle) => 0f;

        public static float GetCastSpeedBonus(in Entity circle) => 0f;

        public static void StartRitual(in Entity circle, FixedString64Bytes ritualName) { }
    }
}

