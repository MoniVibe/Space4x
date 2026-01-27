using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Social;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Systems.Social
{
    /// <summary>
    /// Bridge custody into the agency kernel by ensuring a control link exists from the captor scope to the captive.
    /// This keeps entities blank-by-default: only custody-marked entities receive the agency module.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CustodyCanonicalizationSystem))]
    [UpdateBefore(typeof(AISystemGroup))]
    public partial struct CustodyAgencyBridgeSystem : ISystem
    {
        private BufferLookup<ControlLink> _controlLinkLookup;
        private ComponentLookup<AgencySelf> _agencySelfLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _controlLinkLookup = state.GetBufferLookup<ControlLink>(false);
            _agencySelfLookup = state.GetComponentLookup<AgencySelf>(false);
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
            _agencySelfLookup.Update(ref state);

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (custodyRef, entity) in SystemAPI.Query<RefRO<CustodyState>>().WithEntityAccess())
            {
                var custody = custodyRef.ValueRO;
                var captorScope = custody.CaptorScope;
                if (captorScope == Entity.Null || !em.Exists(captorScope))
                {
                    continue;
                }

                var shouldEnforce = custody.Status == CustodyStatus.Detained ||
                                    custody.Status == CustodyStatus.Negotiating ||
                                    custody.Status == CustodyStatus.Transporting ||
                                    custody.Status == CustodyStatus.Captured;

                var hasLinks = _controlLinkLookup.HasBuffer(entity);
                if (!shouldEnforce && !hasLinks)
                {
                    continue;
                }

                if (shouldEnforce)
                {
                    if (!em.HasComponent<AgencyModuleTag>(entity))
                    {
                        ecb.AddComponent<AgencyModuleTag>(entity);
                    }

                    if (!_agencySelfLookup.HasComponent(entity) && !em.HasComponent<AgencySelf>(entity))
                    {
                        ecb.AddComponent(entity, AgencyDefaults.DefaultSelf());
                    }

                    if (!em.HasBuffer<ResolvedControl>(entity))
                    {
                        ecb.AddBuffer<ResolvedControl>(entity);
                    }
                }

                if (!hasLinks)
                {
                    ecb.AddBuffer<ControlLink>(entity);
                    if (!em.HasBuffer<ResolvedControl>(entity))
                    {
                        ecb.AddBuffer<ResolvedControl>(entity);
                    }

                    ecb.AppendToBuffer(entity, CreateCustodyControlLink(captorScope, custody, tick));
                    continue;
                }

                var links = _controlLinkLookup[entity];
                if (shouldEnforce)
                {
                    UpsertCustodyControlLink(ref links, captorScope, custody, tick);
                }
                else
                {
                    RemoveCustodyControlLink(ref links, captorScope, custody.CapturedTick);
                }
            }

            ecb.Playback(em);
        }

        private static ControlLink CreateCustodyControlLink(Entity captorScope, in CustodyState custody, uint tick)
        {
            var hostility = (custody.Flags & CustodyFlags.HarshTreatment) != 0 ? 0.75f : 0.25f;
            var legitimacy = (custody.Kind == CustodyKind.CriminalDetention) ? 0.75f : 0.35f;

            return new ControlLink
            {
                Controller = captorScope,
                Domains = ResolveCustodyDomains(custody.Kind),
                Pressure = 2.0f,
                Legitimacy = legitimacy,
                Hostility = hostility,
                Consent = math.saturate((custody.Flags & CustodyFlags.HumaneTreatment) != 0 ? 0.1f : 0f),
                EstablishedTick = custody.CapturedTick != 0u ? custody.CapturedTick : tick,
                SourceKind = ControlLinkSourceKind.Custody
            };
        }

        private static AgencyDomain ResolveCustodyDomains(CustodyKind kind)
        {
            // Default: captor controls outward-facing domains while the captive retains inner self-control.
            var domains = AgencyDomain.Movement | AgencyDomain.Work | AgencyDomain.Combat | AgencyDomain.Communications;
            if (kind == CustodyKind.SpyDetention)
            {
                domains |= AgencyDomain.Sensors;
            }

            return domains;
        }

        private static void UpsertCustodyControlLink(
            ref DynamicBuffer<ControlLink> links,
            Entity captorScope,
            in CustodyState custody,
            uint tick)
        {
            var desiredDomains = ResolveCustodyDomains(custody.Kind);

            for (int i = 0; i < links.Length; i++)
            {
                var link = links[i];
                if (link.Controller != captorScope)
                {
                    continue;
                }

                if (link.EstablishedTick != custody.CapturedTick)
                {
                    continue;
                }

                link.Domains = desiredDomains;

                link.Hostility = (custody.Flags & CustodyFlags.HarshTreatment) != 0 ? 0.75f : 0.25f;
                link.Legitimacy = (custody.Kind == CustodyKind.CriminalDetention) ? 0.75f : 0.35f;
                link.Consent = math.saturate((custody.Flags & CustodyFlags.HumaneTreatment) != 0 ? 0.1f : 0f);
                link.Pressure = 2.0f;

                links[i] = link;
                return;
            }

            links.Add(CreateCustodyControlLink(captorScope, custody, tick));
        }

        private static void RemoveCustodyControlLink(ref DynamicBuffer<ControlLink> links, Entity captorScope, uint capturedTick)
        {
            if (links.Length == 0)
            {
                return;
            }

            for (int i = links.Length - 1; i >= 0; i--)
            {
                var link = links[i];
                if (link.Controller == captorScope && link.EstablishedTick == capturedTick)
                {
                    links.RemoveAt(i);
                }
            }
        }
    }
}
