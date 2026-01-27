// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.TimeControl
{
    public static class TimeControlServiceStub
    {
        public static void RequestPause() { }

        public static void RequestResume() { }

        public static void RequestScrub(uint targetTick) { }

        public static void RegisterBookmark(int bookmarkId, uint tick) { }
    }
}
