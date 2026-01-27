using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Agency
{
    /// <summary>
    /// Converts ControlClaim buffers into ControlLink entries and prunes expired claims.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(AISystemGroup))]
    public partial struct AgencyControlClaimBridgeSystem : ISystem
    {
        private BufferLookup<ControlLink> _controlLinkLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _controlLinkLookup = state.GetBufferLookup<ControlLink>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _controlLinkLookup.Update(ref state);

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (claims, entity) in SystemAPI.Query<DynamicBuffer<ControlClaim>>().WithEntityAccess())
            {
                var claimBuffer = claims;
                PruneClaims(ref claimBuffer, em, tick);
                bool hasClaims = claimBuffer.Length > 0;

                if (hasClaims)
                {
                    EnsureAgencyScaffold(entity, em, ref ecb);
                }

                bool hasLinks = _controlLinkLookup.HasBuffer(entity);
                if (!hasLinks)
                {
                    if (hasClaims)
                    {
                        ecb.AddBuffer<ControlLink>(entity);
                        for (int i = 0; i < claimBuffer.Length; i++)
                        {
                            var claim = claimBuffer[i];
                            if (claim.Domains == AgencyDomain.None)
                            {
                                continue;
                            }

                            ecb.AppendToBuffer(entity, CreateLink(claim, tick));
                        }
                    }

                    continue;
                }

                var links = _controlLinkLookup[entity];
                RemoveClaimLinks(ref links);

                if (!hasClaims)
                {
                    continue;
                }

                for (int i = 0; i < claimBuffer.Length; i++)
                {
                    var claim = claimBuffer[i];
                    if (claim.Domains == AgencyDomain.None)
                    {
                        continue;
                    }

                    links.Add(CreateLink(claim, tick));
                }
            }

            ecb.Playback(em);
        }

        private static void PruneClaims(ref DynamicBuffer<ControlClaim> claims, EntityManager em, uint tick)
        {
            for (int i = claims.Length - 1; i >= 0; i--)
            {
                var claim = claims[i];
                if (claim.Controller == Entity.Null || !em.Exists(claim.Controller))
                {
                    claims.RemoveAt(i);
                    continue;
                }

                if (claim.ExpireTick != 0u && tick >= claim.ExpireTick)
                {
                    claims.RemoveAt(i);
                    continue;
                }

                if (claim.EstablishedTick == 0u)
                {
                    claim.EstablishedTick = tick != 0u ? tick : 1u;
                    claims[i] = claim;
                }
            }
        }

        private static void EnsureAgencyScaffold(Entity entity, EntityManager em, ref EntityCommandBuffer ecb)
        {
            if (!em.HasComponent<AgencyModuleTag>(entity))
            {
                ecb.AddComponent<AgencyModuleTag>(entity);
            }

            if (!em.HasComponent<AgencySelf>(entity))
            {
                if (em.HasComponent<AgencySelfPreset>(entity))
                {
                    var preset = em.GetComponentData<AgencySelfPreset>(entity);
                    ecb.AddComponent(entity, AgencySelfPresetUtility.Resolve(preset));
                }
                else
                {
                    ecb.AddComponent(entity, AgencyDefaults.DefaultSelf());
                }
            }

            if (!em.HasBuffer<ResolvedControl>(entity))
            {
                ecb.AddBuffer<ResolvedControl>(entity);
            }
        }

        private static void RemoveClaimLinks(ref DynamicBuffer<ControlLink> links)
        {
            for (int i = links.Length - 1; i >= 0; i--)
            {
                if (links[i].SourceKind == ControlLinkSourceKind.Claim)
                {
                    links.RemoveAt(i);
                }
            }
        }

        private static ControlLink CreateLink(in ControlClaim claim, uint tick)
        {
            return new ControlLink
            {
                Controller = claim.Controller,
                Domains = claim.Domains,
                Pressure = math.max(0f, claim.Pressure),
                Legitimacy = math.saturate(claim.Legitimacy),
                Hostility = math.saturate(claim.Hostility),
                Consent = math.saturate(claim.Consent),
                EstablishedTick = claim.EstablishedTick != 0u ? claim.EstablishedTick : tick,
                SourceKind = ControlLinkSourceKind.Claim
            };
        }
    }
}
