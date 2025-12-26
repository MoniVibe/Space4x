using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Maintains operator control claims for craft based on pilot links.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PureDOTS.Systems.Agency.AgencyControlClaimBridgeSystem))]
    public partial struct Space4XPilotControlClaimSystem : ISystem
    {
        private BufferLookup<ControlClaim> _claimLookup;
        private ComponentLookup<PilotControlClaimConfig> _claimConfigLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _claimLookup = state.GetBufferLookup<ControlClaim>(false);
            _claimConfigLookup = state.GetComponentLookup<PilotControlClaimConfig>(true);
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

            _claimLookup.Update(ref state);
            _claimConfigLookup.Update(ref state);

            var tick = time.Tick;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (pilotLink, entity) in SystemAPI.Query<RefRO<VesselPilotLink>>().WithEntityAccess())
            {
                UpdatePilotClaim(entity, pilotLink.ValueRO.Pilot, tick, ref ecb);
            }

            foreach (var (pilotLink, entity) in SystemAPI.Query<RefRO<StrikeCraftPilotLink>>().WithEntityAccess())
            {
                UpdatePilotClaim(entity, pilotLink.ValueRO.Pilot, tick, ref ecb);
            }

            ecb.Playback(state.EntityManager);
        }

        private void UpdatePilotClaim(Entity entity, Entity pilot, uint tick, ref EntityCommandBuffer ecb)
        {
            if (!_claimLookup.HasBuffer(entity))
            {
                if (pilot == Entity.Null)
                {
                    return;
                }

                var buffer = ecb.AddBuffer<ControlClaim>(entity);
                buffer.Add(CreatePilotClaim(entity, pilot, tick));
                return;
            }

            var claims = _claimLookup[entity];
            if (pilot == Entity.Null)
            {
                RemovePilotClaims(ref claims);
                return;
            }

            UpsertPilotClaim(ref claims, entity, pilot, tick);
        }

        private PilotControlClaimConfig ResolveConfig(Entity entity)
        {
            return _claimConfigLookup.HasComponent(entity)
                ? _claimConfigLookup[entity]
                : PilotControlClaimConfig.Default;
        }

        private ControlClaim CreatePilotClaim(Entity entity, Entity pilot, uint tick)
        {
            var config = ResolveConfig(entity);
            return new ControlClaim
            {
                Controller = pilot,
                SourceSeat = Entity.Null,
                Domains = config.Domains,
                Pressure = config.Pressure,
                Legitimacy = config.Legitimacy,
                Hostility = config.Hostility,
                Consent = config.Consent,
                EstablishedTick = tick,
                ExpireTick = 0u,
                SourceKind = ControlClaimSourceKind.Operator
            };
        }

        private void UpsertPilotClaim(ref DynamicBuffer<ControlClaim> claims, Entity entity, Entity pilot, uint tick)
        {
            int foundIndex = -1;
            for (int i = claims.Length - 1; i >= 0; i--)
            {
                var claim = claims[i];
                if (claim.SourceKind != ControlClaimSourceKind.Operator)
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

            var config = ResolveConfig(entity);
            if (foundIndex >= 0)
            {
                var claim = claims[foundIndex];
                var controllerChanged = claim.Controller != pilot;
                claim.Controller = pilot;
                claim.Domains = config.Domains;
                claim.Pressure = config.Pressure;
                claim.Legitimacy = config.Legitimacy;
                claim.Hostility = config.Hostility;
                claim.Consent = config.Consent;
                claim.ExpireTick = 0u;
                claim.SourceKind = ControlClaimSourceKind.Operator;
                if (claim.EstablishedTick == 0u || controllerChanged)
                {
                    claim.EstablishedTick = tick;
                }

                claims[foundIndex] = claim;
                return;
            }

            claims.Add(CreatePilotClaim(entity, pilot, tick));
        }

        private static void RemovePilotClaims(ref DynamicBuffer<ControlClaim> claims)
        {
            for (int i = claims.Length - 1; i >= 0; i--)
            {
                if (claims[i].SourceKind == ControlClaimSourceKind.Operator)
                {
                    claims.RemoveAt(i);
                }
            }
        }
    }
}
