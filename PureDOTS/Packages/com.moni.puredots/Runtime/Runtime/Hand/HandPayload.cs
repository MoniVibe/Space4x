using Unity.Entities;

namespace PureDOTS.Runtime.Hand
{
    /// <summary>
    /// Buffer on the hand entity that stores resource payload amounts.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct HandPayload : IBufferElementData
    {
        public ushort ResourceTypeIndex;
        public float Amount;
    }
}



