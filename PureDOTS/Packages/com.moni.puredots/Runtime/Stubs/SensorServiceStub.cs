// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Sensors
{
    public static class SensorServiceStub
    {
        public static void RegisterRig(in Entity entity, byte channelsMask) { }

        public static void SubmitInterrupt(in Entity entity, byte category) { }
    }
}
