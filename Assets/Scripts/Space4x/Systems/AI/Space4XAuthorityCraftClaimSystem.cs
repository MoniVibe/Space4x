using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Projects authority seat control claims onto subordinate craft linked to carriers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PureDOTS.Systems.Agency.AgencyControlClaimBridgeSystem))]
    public partial struct Space4XAuthorityCraftClaimSystem : ISystem
    {
        private BufferLookup<ControlClaim> _claimLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private BufferLookup<AuthorityCraftSeatClaim> _seatClaimLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _occupantLookup;
        private ComponentLookup<AuthorityCraftClaimConfig> _configLookup;
        private ComponentLookup<ChildVesselTether> _tetherLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _claimLookup = state.GetBufferLookup<ControlClaim>(false);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatClaimLookup = state.GetBufferLookup<AuthorityCraftSeatClaim>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _occupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _configLookup = state.GetComponentLookup<AuthorityCraftClaimConfig>(true);
            _tetherLookup = state.GetComponentLookup<ChildVesselTether>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<AuthorityCraftClaimToggle>(out var toggle) && toggle.Enabled == 0)
            {
                return;
            }

            _claimLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatClaimLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _occupantLookup.Update(ref state);
            _configLookup.Update(ref state);
            _tetherLookup.Update(ref state);

            var tick = time.Tick;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (profile, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>>().WithEntityAccess())
            {
                var carrier = ResolveCarrier(profile.ValueRO.Carrier, entity);
                ApplyAuthorityClaims(entity, carrier, AuthorityCraftTarget.StrikeCraft, tick, em, ref ecb);
            }

            foreach (var (vessel, entity) in SystemAPI.Query<RefRO<MiningVessel>>().WithEntityAccess())
            {
                var carrier = ResolveCarrier(vessel.ValueRO.CarrierEntity, entity);
                ApplyAuthorityClaims(entity, carrier, AuthorityCraftTarget.MiningVessel, tick, em, ref ecb);
            }

            ecb.Playback(em);
        }

        private Entity ResolveCarrier(Entity declaredCarrier, Entity craftEntity)
        {
            if (declaredCarrier != Entity.Null)
            {
                return declaredCarrier;
            }

            if (_tetherLookup.HasComponent(craftEntity))
            {
                var tether = _tetherLookup[craftEntity];
                if (tether.ParentCarrier != Entity.Null)
                {
                    return tether.ParentCarrier;
                }
            }

            return Entity.Null;
        }

        private void ApplyAuthorityClaims(
            Entity craftEntity,
            Entity carrierEntity,
            AuthorityCraftTarget target,
            uint tick,
            EntityManager em,
            ref EntityCommandBuffer ecb)
        {
            if (carrierEntity == Entity.Null || !em.Exists(carrierEntity))
            {
                return;
            }

            if (!_seatRefLookup.HasBuffer(carrierEntity) || !_seatClaimLookup.HasBuffer(carrierEntity))
            {
                return;
            }

            var seatClaims = _seatClaimLookup[carrierEntity];
            if (seatClaims.Length == 0)
            {
                return;
            }

            var seats = _seatRefLookup[carrierEntity];
            if (seats.Length == 0)
            {
                return;
            }

            var config = _configLookup.HasComponent(carrierEntity)
                ? _configLookup[carrierEntity]
                : AuthorityCraftClaimConfig.Default;
            var durationTicks = config.ClaimDurationTicks > 0 ? config.ClaimDurationTicks : (ushort)2;
            var expireTick = durationTicks > 0 ? tick + durationTicks : 0u;

            DynamicBuffer<ControlClaim> claims;
            if (_claimLookup.HasBuffer(craftEntity))
            {
                claims = _claimLookup[craftEntity];
            }
            else
            {
                claims = ecb.AddBuffer<ControlClaim>(craftEntity);
            }

            for (int i = 0; i < seatClaims.Length; i++)
            {
                var seatClaim = seatClaims[i];
                if (seatClaim.Domains == AgencyDomain.None)
                {
                    continue;
                }

                if ((seatClaim.Targets & target) == 0)
                {
                    continue;
                }

                if (!AuthoritySeatHelpers.TryFindSeatByRole(seats, _seatLookup, seatClaim.RoleId, out var seatEntity))
                {
                    continue;
                }

                if (!_occupantLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                var seat = _seatLookup[seatEntity];
                if (!HasControlRights(seat.Rights))
                {
                    continue;
                }

                var occupant = _occupantLookup[seatEntity];
                if (occupant.OccupantEntity == Entity.Null || !em.Exists(occupant.OccupantEntity))
                {
                    continue;
                }

                var legitimacy = math.saturate(config.BaseLegitimacy + (seat.IsExecutive != 0 ? config.ExecutiveLegitimacyBonus : 0f));
                if (occupant.IsActing != 0)
                {
                    legitimacy *= math.saturate(config.ActingLegitimacyMultiplier);
                }

                legitimacy *= math.max(0f, seatClaim.LegitimacyMultiplier);
                legitimacy *= target == AuthorityCraftTarget.StrikeCraft
                    ? math.max(0f, config.StrikeCraftLegitimacyMultiplier)
                    : math.max(0f, config.MiningVesselLegitimacyMultiplier);
                legitimacy = math.saturate(legitimacy);

                var pressure = math.max(0f, config.BasePressure);
                if ((seat.Rights & AuthoritySeatRights.Execute) != 0)
                {
                    pressure += math.max(0f, config.ExecutePressureBonus);
                }

                if ((seat.Rights & AuthoritySeatRights.Override) != 0)
                {
                    pressure += math.max(0f, config.OverridePressureBonus);
                }

                pressure *= math.max(0f, seatClaim.PressureMultiplier);
                pressure *= target == AuthorityCraftTarget.StrikeCraft
                    ? math.max(0f, config.StrikeCraftPressureMultiplier)
                    : math.max(0f, config.MiningVesselPressureMultiplier);

                var hostility = math.saturate(config.BaseHostility * math.max(0f, seatClaim.HostilityMultiplier));
                var consent = math.saturate(config.BaseConsent * math.max(0f, seatClaim.ConsentMultiplier));

                UpsertAuthorityClaim(
                    ref claims,
                    seatEntity,
                    occupant.OccupantEntity,
                    seatClaim.Domains,
                    pressure,
                    legitimacy,
                    hostility,
                    consent,
                    expireTick,
                    occupant.AssignedTick,
                    tick);
            }
        }

        private static void UpsertAuthorityClaim(
            ref DynamicBuffer<ControlClaim> claims,
            Entity seatEntity,
            Entity controller,
            AgencyDomain domains,
            float pressure,
            float legitimacy,
            float hostility,
            float consent,
            uint expireTick,
            uint assignedTick,
            uint tick)
        {
            int foundIndex = -1;
            for (int i = claims.Length - 1; i >= 0; i--)
            {
                var claim = claims[i];
                if (claim.SourceKind != ControlClaimSourceKind.Authority || claim.SourceSeat != seatEntity)
                {
                    continue;
                }

                if (foundIndex == -1)
                {
                    foundIndex = i;
                }
                else
                {
                    claims.RemoveAt(i);
                }
            }

            if (foundIndex >= 0)
            {
                var claim = claims[foundIndex];
                var controllerChanged = claim.Controller != controller;
                claim.Controller = controller;
                claim.SourceSeat = seatEntity;
                claim.Domains = domains;
                claim.Pressure = math.max(0f, pressure);
                claim.Legitimacy = math.saturate(legitimacy);
                claim.Hostility = math.saturate(hostility);
                claim.Consent = math.saturate(consent);
                claim.ExpireTick = expireTick;
                claim.SourceKind = ControlClaimSourceKind.Authority;
                if (controllerChanged || claim.EstablishedTick == 0u)
                {
                    claim.EstablishedTick = assignedTick != 0u ? assignedTick : tick;
                }

                claims[foundIndex] = claim;
                return;
            }

            claims.Add(new ControlClaim
            {
                Controller = controller,
                SourceSeat = seatEntity,
                Domains = domains,
                Pressure = math.max(0f, pressure),
                Legitimacy = math.saturate(legitimacy),
                Hostility = math.saturate(hostility),
                Consent = math.saturate(consent),
                EstablishedTick = assignedTick != 0u ? assignedTick : tick,
                ExpireTick = expireTick,
                SourceKind = ControlClaimSourceKind.Authority
            });
        }

        private static bool HasControlRights(AuthoritySeatRights rights)
        {
            const AuthoritySeatRights controlRights = AuthoritySeatRights.Execute | AuthoritySeatRights.Override | AuthoritySeatRights.Issue;
            return (rights & controlRights) != 0;
        }
    }
}
