using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// Singleton describing the replay capture stream state.
    /// </summary>
    public struct ReplayCaptureStream : IComponentData
    {
        public uint Version;
        public uint LastTick;
        public int EventCount;
        public ReplayableEvent.EventType LastEventType;
        public FixedString64Bytes LastEventLabel;
    }

    /// <summary>
    /// Lightweight capture event surfaced to tooling and telemetry.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct ReplayCaptureEvent : IBufferElementData
    {
        public uint Tick;
        public ReplayableEvent.EventType Type;
        public FixedString64Bytes Label;
        public float Value;
    }
}
