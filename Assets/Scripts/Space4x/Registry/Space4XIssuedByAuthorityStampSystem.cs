using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Stamps the current captain order with authority attribution (seat + occupant-at-issue-time).
    /// This enables downstream mutiny/refusal/blame/legitimacy logic to reason about "captain via shipmaster" vs "shipmaster under delegated authority".
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XCaptainOrderSystem))]
    public partial struct Space4XIssuedByAuthorityStampSystem : ISystem
    {
        private BufferLookup<AuthorityDelegation> _delegationLookup;
        private ComponentLookup<AuthoritySeatOccupant> _occupantLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CaptainOrder>();
            state.RequireForUpdate<AuthorityBody>();
            state.RequireForUpdate<IssuedByAuthority>();

            _delegationLookup = state.GetBufferLookup<AuthorityDelegation>(true);
            _occupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _delegationLookup.Update(ref state);
            _occupantLookup.Update(ref state);

            foreach (var (order, issuedBy, body, entity) in SystemAPI
                         .Query<RefRW<CaptainOrder>, RefRW<IssuedByAuthority>, RefRO<AuthorityBody>>()
                         .WithEntityAccess())
            {
                var orderValue = order.ValueRO;
                if (orderValue.Type == CaptainOrderType.None || orderValue.Status != CaptainOrderStatus.Received)
                {
                    continue;
                }

                var issuedTick = orderValue.IssuedTick;
                if (issuedTick == 0u)
                {
                    issuedTick = timeState.Tick;
                    order.ValueRW.IssuedTick = issuedTick;
                    orderValue = order.ValueRO;
                }

                if (issuedBy.ValueRO.IssuedTick == issuedTick && issuedBy.ValueRO.ActingSeat == orderValue.IssuingAuthority)
                {
                    continue;
                }

                var principalSeat = body.ValueRO.ExecutiveSeat;
                var actingSeat = orderValue.IssuingAuthority != Entity.Null ? orderValue.IssuingAuthority : principalSeat;
                if (actingSeat == Entity.Null)
                {
                    continue;
                }

                var domain = MapOrderDomain(orderValue.Type);
                var attribution = ResolveAttribution(principalSeat, actingSeat, domain);
                var issuingSeat = actingSeat != principalSeat && attribution == AuthorityAttributionMode.AsPrincipalSeat
                    ? principalSeat
                    : actingSeat;

                issuedBy.ValueRW = new IssuedByAuthority
                {
                    IssuingSeat = issuingSeat,
                    IssuingOccupant = ResolveOccupant(issuingSeat),
                    ActingSeat = actingSeat,
                    ActingOccupant = ResolveOccupant(actingSeat),
                    IssuedTick = issuedTick
                };
            }
        }

        private AuthorityAttributionMode ResolveAttribution(Entity principalSeat, Entity actingSeat, AgencyDomain domain)
        {
            if (principalSeat == Entity.Null || actingSeat == Entity.Null || actingSeat == principalSeat)
            {
                return AuthorityAttributionMode.AsDelegateSeat;
            }

            if (!_delegationLookup.HasBuffer(principalSeat))
            {
                return AuthorityAttributionMode.AsDelegateSeat;
            }

            var delegations = _delegationLookup[principalSeat];
            for (int i = 0; i < delegations.Length; i++)
            {
                var delegation = delegations[i];
                if (delegation.DelegateSeat != actingSeat)
                {
                    continue;
                }

                if ((delegation.Domains & domain) == 0)
                {
                    continue;
                }

                return delegation.Attribution;
            }

            return AuthorityAttributionMode.AsDelegateSeat;
        }

        private Entity ResolveOccupant(Entity seat)
        {
            return _occupantLookup.HasComponent(seat) ? _occupantLookup[seat].OccupantEntity : Entity.Null;
        }

        private static AgencyDomain MapOrderDomain(CaptainOrderType type)
        {
            return type switch
            {
                CaptainOrderType.MoveTo => AgencyDomain.Governance,
                CaptainOrderType.Patrol => AgencyDomain.Governance,
                CaptainOrderType.Escort => AgencyDomain.Governance,
                CaptainOrderType.Retreat => AgencyDomain.Governance,

                CaptainOrderType.Attack => AgencyDomain.Combat,
                CaptainOrderType.Defend => AgencyDomain.Combat,
                CaptainOrderType.Intercept => AgencyDomain.Combat,
                CaptainOrderType.Blockade => AgencyDomain.Combat,

                CaptainOrderType.Mine => AgencyDomain.Logistics,
                CaptainOrderType.Haul => AgencyDomain.Logistics,
                CaptainOrderType.Trade => AgencyDomain.Logistics,
                CaptainOrderType.Resupply => AgencyDomain.Logistics,

                CaptainOrderType.Repair => AgencyDomain.Logistics,
                CaptainOrderType.Rescue => AgencyDomain.Logistics,
                CaptainOrderType.Construct => AgencyDomain.Logistics,
                CaptainOrderType.Survey => AgencyDomain.Logistics,

                CaptainOrderType.Standby => AgencyDomain.Governance,
                CaptainOrderType.Disengage => AgencyDomain.Governance,
                CaptainOrderType.Negotiate => AgencyDomain.Communications,

                _ => AgencyDomain.Governance
            };
        }
    }
}
