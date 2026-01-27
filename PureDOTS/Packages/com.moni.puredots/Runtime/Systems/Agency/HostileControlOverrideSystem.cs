using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Agency
{
    /// <summary>
    /// Emits hostile control claims based on HostileControlOverride components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(AgencyControlClaimBridgeSystem))]
    public partial struct HostileControlOverrideSystem : ISystem
    {
        private BufferLookup<ControlClaim> _claimLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _claimLookup = state.GetBufferLookup<ControlClaim>(false);
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

            var tick = time.Tick;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (overrideRef, entity) in SystemAPI.Query<RefRW<HostileControlOverride>>().WithEntityAccess())
            {
                var data = overrideRef.ValueRO;
                if (data.Active == 0 || data.Controller == Entity.Null || data.Domains == AgencyDomain.None)
                {
                    if (_claimLookup.HasBuffer(entity))
                    {
                        var claimBuffer = _claimLookup[entity];
                        RemoveHostileClaims(ref claimBuffer);
                    }

                    if (data.Active != 0 && (data.Controller == Entity.Null || data.Domains == AgencyDomain.None))
                    {
                        overrideRef.ValueRW.Active = 0;
                    }

                    overrideRef.ValueRW.EstablishedTick = 0u;
                    overrideRef.ValueRW.ExpireTick = 0u;
                    continue;
                }

                if (!em.Exists(data.Controller))
                {
                    if (_claimLookup.HasBuffer(entity))
                    {
                        var claimBuffer = _claimLookup[entity];
                        RemoveHostileClaims(ref claimBuffer);
                    }

                    overrideRef.ValueRW.Active = 0;
                    overrideRef.ValueRW.EstablishedTick = 0u;
                    overrideRef.ValueRW.ExpireTick = 0u;
                    continue;
                }

                var establishedTick = data.EstablishedTick;
                if (establishedTick == 0u)
                {
                    establishedTick = tick != 0u ? tick : 1u;
                    overrideRef.ValueRW.EstablishedTick = establishedTick;
                }

                var expireTick = data.ExpireTick;
                if (expireTick == 0u && data.DurationTicks > 0u)
                {
                    expireTick = establishedTick + data.DurationTicks;
                    overrideRef.ValueRW.ExpireTick = expireTick;
                }

                if (expireTick != 0u && tick >= expireTick)
                {
                    overrideRef.ValueRW.Active = 0;
                    overrideRef.ValueRW.EstablishedTick = 0u;
                    overrideRef.ValueRW.ExpireTick = 0u;
                    if (_claimLookup.HasBuffer(entity))
                    {
                        var claimBuffer = _claimLookup[entity];
                        RemoveHostileClaims(ref claimBuffer);
                    }
                    continue;
                }

                DynamicBuffer<ControlClaim> claims;
                if (_claimLookup.HasBuffer(entity))
                {
                    claims = _claimLookup[entity];
                }
                else
                {
                    claims = ecb.AddBuffer<ControlClaim>(entity);
                }

                UpsertHostileClaim(ref claims, data, tick, expireTick);
            }

            ecb.Playback(em);
        }

        private static void RemoveHostileClaims(ref DynamicBuffer<ControlClaim> claims)
        {
            for (int i = claims.Length - 1; i >= 0; i--)
            {
                if (claims[i].SourceKind == ControlClaimSourceKind.Hostile)
                {
                    claims.RemoveAt(i);
                }
            }
        }

        private static void UpsertHostileClaim(ref DynamicBuffer<ControlClaim> claims, in HostileControlOverride data, uint tick, uint expireTick)
        {
            int foundIndex = -1;
            for (int i = claims.Length - 1; i >= 0; i--)
            {
                if (claims[i].SourceKind != ControlClaimSourceKind.Hostile)
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

            var establishedTick = data.EstablishedTick != 0u ? data.EstablishedTick : tick;
            if (foundIndex >= 0)
            {
                var claim = claims[foundIndex];
                var controllerChanged = claim.Controller != data.Controller;
                claim.Controller = data.Controller;
                claim.SourceSeat = Entity.Null;
                claim.Domains = data.Domains;
                claim.Pressure = math.max(0f, data.Pressure);
                claim.Legitimacy = math.saturate(data.Legitimacy);
                claim.Hostility = math.saturate(data.Hostility);
                claim.Consent = math.saturate(data.Consent);
                claim.ExpireTick = expireTick;
                claim.SourceKind = ControlClaimSourceKind.Hostile;
                if (controllerChanged || claim.EstablishedTick == 0u)
                {
                    claim.EstablishedTick = establishedTick;
                }

                claims[foundIndex] = claim;
                return;
            }

            claims.Add(new ControlClaim
            {
                Controller = data.Controller,
                SourceSeat = Entity.Null,
                Domains = data.Domains,
                Pressure = math.max(0f, data.Pressure),
                Legitimacy = math.saturate(data.Legitimacy),
                Hostility = math.saturate(data.Hostility),
                Consent = math.saturate(data.Consent),
                EstablishedTick = establishedTick,
                ExpireTick = expireTick,
                SourceKind = ControlClaimSourceKind.Hostile
            });
        }
    }
}
