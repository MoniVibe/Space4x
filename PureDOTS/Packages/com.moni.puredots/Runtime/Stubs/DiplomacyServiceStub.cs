// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Diplomacy
{
    public static class DiplomacyServiceStub
    {
        public static void ApplyRelationDelta(in Entity a, in Entity b, float delta) { }

        public static float GetRelation(in Entity a, in Entity b) => 0f;
    }
}
