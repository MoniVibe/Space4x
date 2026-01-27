using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Signals
{
    /// <summary>
    /// Global append-only signal stream for UI hooks and cross-system triggers.
    /// </summary>
    public struct SignalBus : IComponentData
    {
        public uint Version;
        public uint LastWriteTick;
        public int PendingCount;
        public int DroppedCount;
    }

    public struct SignalBusConfig : IComponentData
    {
        public int MaxSignals;

        public static SignalBusConfig CreateDefault(int maxSignals = 256)
        {
            return new SignalBusConfig
            {
                MaxSignals = maxSignals <= 0 ? 256 : maxSignals
            };
        }
    }

    public struct SignalEvent : IBufferElementData
    {
        public FixedString64Bytes Channel;
        public FixedString128Bytes Payload;
        public float3 Position;
        public Entity Source;
        public byte Severity;
        public uint Tick;
    }
}
