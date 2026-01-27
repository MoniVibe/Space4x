// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    public static class SocialStatsStub
    {
        public static void SetFame(in Entity entity, float fame) { }

        public static void AddFame(in Entity entity, float delta) { }

        public static float GetFame(in Entity entity) => 0f;

        public static void SetWealth(in Entity entity, float wealth) { }

        public static void AddWealth(in Entity entity, float delta) { }

        public static float GetWealth(in Entity entity) => 0f;

        public static void SetReputation(in Entity entity, float reputation) { }

        public static void AddReputation(in Entity entity, float delta) { }

        public static float GetReputation(in Entity entity) => 0f;

        public static void SetGlory(in Entity entity, float glory) { }

        public static void AddGlory(in Entity entity, float delta) { }

        public static float GetGlory(in Entity entity) => 0f;

        public static void SetRenown(in Entity entity, float renown) { }

        public static void AddRenown(in Entity entity, float delta) { }

        public static float GetRenown(in Entity entity) => 0f;

        public static bool IsLegendary(in Entity entity) => false;
    }
}

