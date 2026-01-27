using System;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Identity
{
    [Flags]
    public enum AuthorityCapabilityFlags : uint
    {
        None = 0,
        IssueOrders = 1u << 0,
        OverrideSafetyProtocols = 1u << 1,
        UseLethalForce = 1u << 2,
        DeclareEmergency = 1u << 3,
        AccessRestrictedKnowledge = 1u << 4,
        CollectivePunishment = 1u << 5,
        SuspendRights = 1u << 6
    }

    /// <summary>
    /// Capabilities held by an authority seat or entity.
    /// </summary>
    public struct AuthorityCapabilities : IComponentData
    {
        public AuthorityCapabilityFlags Flags;
    }

    /// <summary>
    /// Authority seat descriptor linking capability sets to host seats.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct AuthoritySeat : IBufferElementData
    {
        public FixedString64Bytes SeatId;
        public AuthorityCapabilityFlags Flags;
        public Entity CurrentHolder;
    }
}

