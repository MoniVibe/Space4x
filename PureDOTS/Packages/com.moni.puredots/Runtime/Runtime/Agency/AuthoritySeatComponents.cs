using System;
using PureDOTS.Runtime.Agency;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Authority
{
    /// <summary>
    /// Governance model for an authority body (single executive vs council/quorum).
    /// </summary>
    public enum AuthorityBodyMode : byte
    {
        SingleExecutive = 0,
        Council = 1
    }

    /// <summary>
    /// Rights a seat has in its domains (recommend/issue/execute/override).
    /// </summary>
    [Flags]
    public enum AuthoritySeatRights : byte
    {
        None = 0,
        Recommend = 1 << 0,
        Issue = 1 << 1,
        Execute = 1 << 2,
        Override = 1 << 3
    }

    /// <summary>
    /// When a delegation becomes active.
    /// </summary>
    public enum AuthorityDelegationCondition : byte
    {
        Always = 0,
        WhenPrincipalVacant = 1,
        WhenPrincipalUnavailable = 2,
        WhenExplicitlyActivated = 3
    }

    /// <summary>
    /// Attribution model for delegated actions (whose "name" the order is issued under).
    /// </summary>
    public enum AuthorityAttributionMode : byte
    {
        AsDelegateSeat = 0,
        AsPrincipalSeat = 1
    }

    /// <summary>
    /// Root authority body attached to an aggregate entity (village, ship, army, etc).
    /// Seats are separate entities referenced by <see cref="AuthoritySeatRef"/>.
    /// </summary>
    public struct AuthorityBody : IComponentData
    {
        public AuthorityBodyMode Mode;

        /// <summary>
        /// The executive seat (mayor/captain/lord) if present; can be null for council-only bodies.
        /// </summary>
        public Entity ExecutiveSeat;

        public uint CreatedTick;
    }

    /// <summary>
    /// Buffer of seats owned by an authority body.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct AuthoritySeatRef : IBufferElementData
    {
        public Entity SeatEntity;
    }

    /// <summary>
    /// A governance seat (role) within an authority body.
    /// </summary>
    public struct AuthoritySeat : IComponentData
    {
        public Entity BodyEntity;
        public FixedString64Bytes RoleId;
        public AgencyDomain Domains;
        public AuthoritySeatRights Rights;
        public byte IsExecutive;
        public byte Reserved0;
        public ushort Reserved1;
    }

    /// <summary>
    /// Current occupant of a seat (entity can change over time; seat entity stays stable).
    /// </summary>
    public struct AuthoritySeatOccupant : IComponentData
    {
        public Entity OccupantEntity;
        public uint AssignedTick;
        public uint LastChangedTick;
        public byte IsActing;
        public byte Reserved0;
        public ushort Reserved1;
    }

    /// <summary>
    /// Delegation edge from a principal seat to a delegate seat.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct AuthorityDelegation : IBufferElementData
    {
        public Entity DelegateSeat;
        public AgencyDomain Domains;
        public AuthoritySeatRights GrantedRights;
        public AuthorityDelegationCondition Condition;
        public AuthorityAttributionMode Attribution;
        public byte Reserved0;
        public ushort Reserved1;
    }

    /// <summary>
    /// Optional attribution component for orders/actions issued by an authority seat.
    /// Keeps seat identity stable while capturing occupant-at-issue-time for history/telemetry.
    /// </summary>
    public struct IssuedByAuthority : IComponentData
    {
        public Entity IssuingSeat;
        public Entity IssuingOccupant;
        public Entity ActingSeat;
        public Entity ActingOccupant;
        public uint IssuedTick;
    }

    public static class AuthoritySeatDefaults
    {
        public static AuthoritySeat CreateExecutive(Entity body, in FixedString64Bytes roleId, AgencyDomain domains)
        {
            return new AuthoritySeat
            {
                BodyEntity = body,
                RoleId = roleId,
                Domains = domains,
                Rights = AuthoritySeatRights.Recommend | AuthoritySeatRights.Issue | AuthoritySeatRights.Execute | AuthoritySeatRights.Override,
                IsExecutive = 1
            };
        }

        public static AuthoritySeat CreateDelegate(Entity body, in FixedString64Bytes roleId, AgencyDomain domains, AuthoritySeatRights rights)
        {
            return new AuthoritySeat
            {
                BodyEntity = body,
                RoleId = roleId,
                Domains = domains,
                Rights = rights,
                IsExecutive = 0
            };
        }

        public static AuthoritySeatOccupant Vacant(uint tick)
        {
            return new AuthoritySeatOccupant
            {
                OccupantEntity = Entity.Null,
                AssignedTick = tick,
                LastChangedTick = tick,
                IsActing = 0
            };
        }
    }

    public static class AuthoritySeatHelpers
    {
        public static bool TryFindSeatByRole(
            DynamicBuffer<AuthoritySeatRef> seats,
            ComponentLookup<AuthoritySeat> seatLookup,
            in FixedString64Bytes roleId,
            out Entity seatEntity)
        {
            seatEntity = Entity.Null;
            for (int i = 0; i < seats.Length; i++)
            {
                var candidate = seats[i].SeatEntity;
                if (candidate == Entity.Null || !seatLookup.HasComponent(candidate))
                {
                    continue;
                }

                if (seatLookup[candidate].RoleId.Equals(roleId))
                {
                    seatEntity = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}

