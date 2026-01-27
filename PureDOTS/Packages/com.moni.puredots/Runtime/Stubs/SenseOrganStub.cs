// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Perception
{
    public static class SenseOrganStub
    {
        public static float GetOrganGain(in Entity entity, SenseOrganType organType) => 1f;

        public static float GetOrganCondition(in Entity entity, SenseOrganType organType) => 1f;

        public static float GetOrganNoiseFloor(in Entity entity, SenseOrganType organType) => 0f;

        public static float GetEffectiveRange(in Entity entity, SenseOrganType organType, float baseRange) => baseRange;
    }
}

