// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    public struct TelemetryStreamId : IComponentData
    {
        public int Value;
    }

    public struct TelemetrySample : IBufferElementData
    {
        public FixedString64Bytes Metric;
        public float Value;
    }
}
