// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Collections;

namespace PureDOTS.Runtime.Telemetry
{
    public static class TelemetryBridgeStub
    {
        public static void RecordMetric(FixedString64Bytes name, float value) { }

        public static void RecordEvent(FixedString64Bytes eventId) { }
    }
}
