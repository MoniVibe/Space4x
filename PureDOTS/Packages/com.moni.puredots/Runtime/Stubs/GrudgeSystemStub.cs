// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Relations
{
    public static class GrudgeSystemStub
    {
        public static void CreateGrudge(in Entity source, in Entity target, GrudgeType type, float intensity) { }

        public static float GetGrudgeIntensity(in Entity source, in Entity target) => 0f;

        public static bool HasGrudge(in Entity source, in Entity target) => false;

        public static void ResolveGrudge(in Entity source, in Entity target) { }
    }
}

