using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Resources
{
    public enum RequestFailureReason : byte
    {
        None = 0,
        InvalidRequester = 1,
        InvalidTarget = 2,
        NoSupply = 3,
        ReservationFailed = 4,
        Expired = 5,
        Cancelled = 6,
        RouteUnavailable = 7,
        NoCarrier = 8,
        InvalidContainer = 9,
        CapacityFull = 10
    }

    /// <summary>
    /// A request for resources by an agent or group.
    /// Describes what is needed, who needs it, and priority.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct NeedRequest : IBufferElementData
    {
        /// <summary>
        /// Resource type identifier.
        /// </summary>
        public FixedString32Bytes ResourceTypeId;

        /// <summary>
        /// Amount needed.
        /// </summary>
        public float Amount;

        /// <summary>
        /// Entity that needs this resource.
        /// </summary>
        public Entity RequesterEntity;

        /// <summary>
        /// Priority (higher = more urgent).
        /// </summary>
        public float Priority;

        /// <summary>
        /// Tick when request was created.
        /// </summary>
        public uint CreatedTick;

        /// <summary>
        /// Optional target entity (e.g., storehouse to deliver to).
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Request ID for tracking fulfillment.
        /// </summary>
        public uint RequestId;

        /// <summary>
        /// Linked logistics order entity for fulfillment.
        /// </summary>
        public Entity OrderEntity;

        /// <summary>
        /// Failure reason for this request, if any.
        /// </summary>
        public RequestFailureReason FailureReason;
    }
}



