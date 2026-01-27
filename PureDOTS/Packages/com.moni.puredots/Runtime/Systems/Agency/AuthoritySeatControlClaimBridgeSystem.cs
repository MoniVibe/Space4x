using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Agency
{
    /// <summary>
    /// Emits authority seat control claims onto the authority body entity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(AISystemGroup))]
    [UpdateBefore(typeof(AgencyControlClaimBridgeSystem))]
    public partial struct AuthoritySeatControlClaimBridgeSystem : ISystem
    {
        private BufferLookup<ControlClaim> _claimLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _occupantLookup;
        private ComponentLookup<AuthorityControlClaimConfig> _configLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _claimLookup = state.GetBufferLookup<ControlClaim>(false);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _occupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _configLookup = state.GetComponentLookup<AuthorityControlClaimConfig>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _claimLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _occupantLookup.Update(ref state);
            _configLookup.Update(ref state);

            var tick = timeState.Tick;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<AuthorityBody>>().WithEntityAccess())
            {
                if (!_seatRefLookup.HasBuffer(entity))
                {
                    RemoveAuthorityClaims(entity);
                    continue;
                }

                var seats = _seatRefLookup[entity];
                if (seats.Length == 0)
                {
                    RemoveAuthorityClaims(entity);
                    continue;
                }

                if (!_claimLookup.HasBuffer(entity))
                {
                    var buffer = ecb.AddBuffer<ControlClaim>(entity);
                    AddAuthorityClaims(ref buffer, entity, seats, tick, em);
                    continue;
                }

                var claims = _claimLookup[entity];
                RemoveAuthorityClaims(ref claims);
                AddAuthorityClaims(ref claims, entity, seats, tick, em);
            }

            ecb.Playback(em);
        }

        private void RemoveAuthorityClaims(Entity entity)
        {
            if (!_claimLookup.HasBuffer(entity))
            {
                return;
            }

            var claims = _claimLookup[entity];
            RemoveAuthorityClaims(ref claims);
        }

        private static void RemoveAuthorityClaims(ref DynamicBuffer<ControlClaim> claims)
        {
            for (int i = claims.Length - 1; i >= 0; i--)
            {
                if (claims[i].SourceKind == ControlClaimSourceKind.Authority)
                {
                    claims.RemoveAt(i);
                }
            }
        }

        private void AddAuthorityClaims(
            ref DynamicBuffer<ControlClaim> claims,
            Entity bodyEntity,
            DynamicBuffer<AuthoritySeatRef> seats,
            uint tick,
            EntityManager em)
        {
            var config = ResolveConfig(bodyEntity);
            for (int i = 0; i < seats.Length; i++)
            {
                var seatEntity = seats[i].SeatEntity;
                if (seatEntity == Entity.Null || !_seatLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                if (!_occupantLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                var occupant = _occupantLookup[seatEntity];
                if (occupant.OccupantEntity == Entity.Null || !em.Exists(occupant.OccupantEntity))
                {
                    continue;
                }

                var seat = _seatLookup[seatEntity];
                if (seat.Domains == AgencyDomain.None || !HasControlRights(seat.Rights))
                {
                    continue;
                }

                var legitimacy = math.saturate(config.BaseLegitimacy + (seat.IsExecutive != 0 ? config.ExecutiveLegitimacyBonus : 0f));
                if (occupant.IsActing != 0)
                {
                    legitimacy *= math.saturate(config.ActingLegitimacyMultiplier);
                }

                var pressure = math.max(0f, config.BasePressure);
                if ((seat.Rights & AuthoritySeatRights.Execute) != 0)
                {
                    pressure += math.max(0f, config.ExecutePressureBonus);
                }

                if ((seat.Rights & AuthoritySeatRights.Override) != 0)
                {
                    pressure += math.max(0f, config.OverridePressureBonus);
                }

                claims.Add(new ControlClaim
                {
                    Controller = occupant.OccupantEntity,
                    SourceSeat = seatEntity,
                    Domains = seat.Domains,
                    Pressure = pressure,
                    Legitimacy = legitimacy,
                    Hostility = math.saturate(config.BaseHostility),
                    Consent = math.saturate(config.BaseConsent),
                    EstablishedTick = occupant.AssignedTick != 0u ? occupant.AssignedTick : tick,
                    ExpireTick = 0u,
                    SourceKind = ControlClaimSourceKind.Authority
                });
            }
        }

        private AuthorityControlClaimConfig ResolveConfig(Entity bodyEntity)
        {
            return _configLookup.HasComponent(bodyEntity)
                ? _configLookup[bodyEntity]
                : AuthorityControlClaimConfig.Default;
        }

        private static bool HasControlRights(AuthoritySeatRights rights)
        {
            const AuthoritySeatRights controlRights = AuthoritySeatRights.Execute | AuthoritySeatRights.Override | AuthoritySeatRights.Issue;
            return (rights & controlRights) != 0;
        }
    }
}
