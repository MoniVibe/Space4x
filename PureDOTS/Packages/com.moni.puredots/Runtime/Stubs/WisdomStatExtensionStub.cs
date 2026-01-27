// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    public static class WisdomStatExtensionStub
    {
        public static float GetWisdom(in Entity entity) => 0f;

        public static void SetWisdom(in Entity entity, float wisdom) { }

        public static float GetWisdomGainModifier(in Entity entity) => 1f;
    }
}

