// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Cooperation
{
    public static class MutualCareStub
    {
        public static void CreateCareRelationship(in Entity caregiver, in Entity receiver, CareRelationshipType type) { }

        public static float GetCareLevel(in Entity caregiver, in Entity receiver) => 0f;

        public static void PerformCareAction(in Entity caregiver, in Entity receiver, CareActionType action) { }

        public static float GetBondStrength(in Entity entityA, in Entity entityB) => 0f;
    }
}

