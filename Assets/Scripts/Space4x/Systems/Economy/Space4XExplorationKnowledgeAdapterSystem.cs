using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Space4X-specific adapter that seeds reusable site-knowledge policy/state onto economy entities.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XExplorationKnowledgeAdapterSystem : ISystem
    {
        private const uint AdapterStrideTicks = 64u;

        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<Carrier> _carrierLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SiteKnowledgeRuntimeSettings>();

            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<SiteKnowledgeRuntimeSettings>(out var settings) || settings.Enabled == 0)
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused || time.Tick % AdapterStrideTicks != 0u)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _affiliationLookup.Update(ref state);
            _carrierLookup.Update(ref state);

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var anyChanges = false;

            foreach (var (_, entity) in SystemAPI.Query<RefRO<ResourceSourceState>>().WithEntityAccess())
            {
                var owner = ResolveOperationalOwner(entity);
                var policy = BuildResourcePolicy(owner);
                anyChanges |= EnsureSiteKnowledgeSetup(
                    em,
                    ref ecb,
                    entity,
                    owner,
                    in policy,
                    settings.MinSeedKnowledge01,
                    0.22f);
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<ProcessingFacility>>().WithEntityAccess())
            {
                var owner = ResolveOperationalOwner(entity);
                var policy = BuildFacilityPolicy(owner);
                anyChanges |= EnsureSiteKnowledgeSetup(
                    em,
                    ref ecb,
                    entity,
                    owner,
                    in policy,
                    settings.MinSeedKnowledge01,
                    0.35f);
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlanetFlavorComponent>>().WithEntityAccess())
            {
                var owner = ResolveOperationalOwner(entity);
                var policy = BuildPlanetPolicy(owner);
                anyChanges |= EnsureSiteKnowledgeSetup(
                    em,
                    ref ecb,
                    entity,
                    owner,
                    in policy,
                    settings.MinSeedKnowledge01,
                    0.12f);
            }

            if (anyChanges)
            {
                ecb.Playback(em);
            }
        }

        private Entity ResolveOperationalOwner(Entity entity)
        {
            if (_carrierLookup.HasComponent(entity))
            {
                var carrier = _carrierLookup[entity];
                if (carrier.AffiliationEntity != Entity.Null)
                {
                    return carrier.AffiliationEntity;
                }
            }

            if (!_affiliationLookup.HasBuffer(entity))
            {
                return Entity.Null;
            }

            var affiliations = _affiliationLookup[entity];
            for (int i = 0; i < affiliations.Length; i++)
            {
                var tag = affiliations[i];
                if (tag.Target == Entity.Null)
                {
                    continue;
                }

                if (tag.Type == AffiliationType.Faction ||
                    tag.Type == AffiliationType.Empire ||
                    tag.Type == AffiliationType.Corporation ||
                    tag.Type == AffiliationType.Colony)
                {
                    return tag.Target;
                }
            }

            return Entity.Null;
        }

        private static SiteAccessPolicy BuildResourcePolicy(Entity owner)
        {
            return new SiteAccessPolicy
            {
                LegalOwner = owner,
                DefaultRights = SiteAccessRights.All,
                UnauthorizedRights = SiteAccessRights.Survey | SiteAccessRights.Extract,
                UnauthorizedExtractionMultiplier = 0.6f,
                UnauthorizedProcessingMultiplier = 0.75f,
                KnowledgeThreshold01 = 0.5f,
                RegulationSeverity01 = owner == Entity.Null ? 0f : 0.45f
            };
        }

        private static SiteAccessPolicy BuildFacilityPolicy(Entity owner)
        {
            return new SiteAccessPolicy
            {
                LegalOwner = owner,
                DefaultRights = SiteAccessRights.All,
                UnauthorizedRights = SiteAccessRights.Survey | SiteAccessRights.Process,
                UnauthorizedExtractionMultiplier = 0.7f,
                UnauthorizedProcessingMultiplier = 0.72f,
                KnowledgeThreshold01 = 0.4f,
                RegulationSeverity01 = owner == Entity.Null ? 0f : 0.35f
            };
        }

        private static SiteAccessPolicy BuildPlanetPolicy(Entity owner)
        {
            return new SiteAccessPolicy
            {
                LegalOwner = owner,
                DefaultRights = SiteAccessRights.Survey,
                UnauthorizedRights = SiteAccessRights.Survey,
                UnauthorizedExtractionMultiplier = 0.55f,
                UnauthorizedProcessingMultiplier = 0.65f,
                KnowledgeThreshold01 = 0.55f,
                RegulationSeverity01 = owner == Entity.Null ? 0f : 0.5f
            };
        }

        private static bool EnsureSiteKnowledgeSetup(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity site,
            Entity owner,
            in SiteAccessPolicy policy,
            float minSeedKnowledge,
            float ownerSeedKnowledge)
        {
            var changed = false;
            if (!em.HasComponent<SiteAccessPolicy>(site))
            {
                ecb.AddComponent(site, policy);
                changed = true;
            }
            else if (owner != Entity.Null)
            {
                var existing = em.GetComponentData<SiteAccessPolicy>(site);
                if (existing.LegalOwner == Entity.Null)
                {
                    existing.LegalOwner = owner;
                    em.SetComponentData(site, existing);
                    changed = true;
                }
            }

            if (!em.HasComponent<SiteKnowledgeState>(site))
            {
                var globalSeed = owner != Entity.Null
                    ? math.max(minSeedKnowledge, ownerSeedKnowledge * 0.6f)
                    : minSeedKnowledge;
                ecb.AddComponent(site, new SiteKnowledgeState
                {
                    GlobalKnowledge01 = math.saturate(globalSeed),
                    LastObservationStrength = 0f,
                    LastUpdateTick = 0u
                });
                changed = true;
            }

            if (!em.HasBuffer<SiteKnowledgeByOwner>(site))
            {
                ecb.AddBuffer<SiteKnowledgeByOwner>(site);
                changed = true;
            }
            else if (owner != Entity.Null)
            {
                var ownerKnowledge = em.GetBuffer<SiteKnowledgeByOwner>(site);
                var found = false;
                for (int i = 0; i < ownerKnowledge.Length; i++)
                {
                    if (ownerKnowledge[i].Owner == owner)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    ownerKnowledge.Add(new SiteKnowledgeByOwner
                    {
                        Owner = owner,
                        Knowledge01 = math.saturate(ownerSeedKnowledge),
                        LastObservedTick = 0u
                    });
                    changed = true;
                }
            }

            if (!em.HasBuffer<SiteAccessGrant>(site))
            {
                ecb.AddBuffer<SiteAccessGrant>(site);
                changed = true;
            }
            else if (owner != Entity.Null)
            {
                var grants = em.GetBuffer<SiteAccessGrant>(site);
                var found = false;
                for (int i = 0; i < grants.Length; i++)
                {
                    if (grants[i].Owner == owner)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    grants.Add(new SiteAccessGrant
                    {
                        Owner = owner,
                        Rights = SiteAccessRights.All,
                        ExtractionMultiplier = 1f,
                        ProcessingMultiplier = 1f
                    });
                    changed = true;
                }
            }

            return changed;
        }
    }
}
