// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Relations
{
    public static class TrustSystemStub
    {
        public static void RecordTrustEvent(in Entity source, in Entity target, TrustEventType eventType) { }

        public static float CalculateTrustLevel(in Entity source, in Entity target) => 50f;

        public static float GetTrustScore(in Entity source, in Entity target) => 50f;

        public static void UpdateTrustFromReliability(in Entity source, in Entity target, float reliabilityScore) { }
    }
}

