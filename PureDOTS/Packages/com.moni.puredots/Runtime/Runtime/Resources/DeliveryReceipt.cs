using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Resources
{
    /// <summary>
    /// Receipt confirming delivery of resources.
    /// Links back to the original NeedRequest.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DeliveryReceipt : IBufferElementData
    {
        /// <summary>
        /// Request ID that was fulfilled.
        /// </summary>
        public uint RequestId;

        /// <summary>
        /// Amount actually delivered.
        /// </summary>
        public float DeliveredAmount;

        /// <summary>
        /// Entity that delivered the resources.
        /// </summary>
        public Entity DelivererEntity;

        /// <summary>
        /// Entity that received the resources.
        /// </summary>
        public Entity RecipientEntity;

        /// <summary>
        /// Tick when delivery occurred.
        /// </summary>
        public uint DeliveryTick;

        /// <summary>
        /// Resource type that was delivered.
        /// </summary>
        public FixedString32Bytes ResourceTypeId;
    }
}



