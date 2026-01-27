using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Light-weight key/value store per situation instance.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SituationFlag : IBufferElementData
    {
        public int Key;
        public int Value;
    }
}

