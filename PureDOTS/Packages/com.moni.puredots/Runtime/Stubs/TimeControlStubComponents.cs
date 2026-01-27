// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.TimeControl
{
    public struct TimeControlCommand : IComponentData
    {
        public byte CommandType; // pause/play/scrub
        public uint TargetTick;
    }

    public struct TimelineBookmark : IComponentData
    {
        public uint Tick;
        public int BookmarkId;
    }

    public struct PlaybackMarker : IComponentData
    {
        public uint CurrentTick;
    }
}
