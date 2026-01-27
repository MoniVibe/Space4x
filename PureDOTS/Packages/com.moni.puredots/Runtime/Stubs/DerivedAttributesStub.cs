// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    public static class DerivedAttributesStub
    {
        public static float CalculateStrength(in Entity entity) => 0f;

        public static float CalculateAgility(in Entity entity) => 0f;

        public static float CalculateIntelligence(in Entity entity) => 0f;

        public static float CalculateWisdomDerived(in Entity entity) => 0f;

        public static void UpdateDerivedAttributes(in Entity entity) { }

        public static void RecalculateAllDerivedAttributes() { }
    }
}

