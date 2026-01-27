// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Deception
{
    public static class DeceptionServiceStub
    {
        public static bool AttemptDeception(in Entity deceiver, in Entity target, DeceptionType type) => false;

        public static bool DetectDeception(in Entity detector, in Entity suspect) => false;

        public static float CalculateDeceptionSuccess(in Entity deceiver, in Entity target, float clarity, float languageProficiency) => 0f;
    }
}

