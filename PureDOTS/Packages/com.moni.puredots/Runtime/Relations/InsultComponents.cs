using Unity.Entities;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Insult type.
    /// </summary>
    public enum InsultType : byte
    {
        Verbal = 0,
        Gesture = 1,
        RefusalToCooperate = 2
    }

    /// <summary>
    /// Insult event buffer element.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct InsultEvent : IBufferElementData
    {
        public Entity Target;
        public InsultType Type;
        public byte Severity; // 1-10
        public uint Tick;
    }
}

