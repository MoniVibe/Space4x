// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Perception
{
    public static class PerceptionChannelStub
    {
        public static bool DetectViaChannel(in Entity detector, in Entity target, PerceptionChannel channel) => false;

        public static float GetChannelConfidence(in Entity detector, in Entity target, PerceptionChannel channel) => 0f;

        public static PerceptionChannel GetDetectedChannels(in Entity detector, in Entity target) => PerceptionChannel.None;
    }
}

