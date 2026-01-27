// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    public static class XPStatsStub
    {
        public static void AddPhysiqueXP(in Entity entity, float amount) { }

        public static void AddFinesseXP(in Entity entity, float amount) { }

        public static void AddWillXP(in Entity entity, float amount) { }

        public static void AddWisdomXP(in Entity entity, float amount) { }

        public static float GetPhysiqueXP(in Entity entity) => 0f;

        public static float GetFinesseXP(in Entity entity) => 0f;

        public static float GetWillXP(in Entity entity) => 0f;

        public static float GetWisdomXP(in Entity entity) => 0f;

        public static void SpendXP(in Entity entity, XPType type, float amount) { }

        public static void DecayXP(in Entity entity, float deltaTime) { }
    }
}

