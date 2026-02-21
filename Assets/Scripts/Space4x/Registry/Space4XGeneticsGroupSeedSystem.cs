using PureDOTS.Runtime;
using PureDOTS.Runtime.Genetics;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Seeds baseline genetics and culture profiles onto aggregate entities (colonies, factions).
    /// Data-only: uses catalog defaults and does not resolve dominant species yet.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XGeneticsSeedSystem))]
    public partial struct Space4XGeneticsGroupSeedSystem : ISystem
    {
        private EntityQuery _colonyQuery;
        private EntityQuery _factionQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();

            _colonyQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XColony>()
                .Build();

            _factionQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XFaction>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            if (_colonyQuery.IsEmptyIgnoreFilter && _factionQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var catalog = Space4XGeneticsCatalog.LoadOrFallback();
            var geneticProfile = ResolveGeneticProfile(catalog, "genetic.baseline");
            var cultureProfile = ResolveCultureProfile(catalog, "culture.baseline");

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (colony, entity) in SystemAPI.Query<RefRO<Space4XColony>>().WithEntityAccess())
            {
                SeedGroupProfiles(em, ecb, entity, geneticProfile, cultureProfile, colony.ValueRO.ColonyId);
            }

            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                var id = new FixedString64Bytes("space4x.faction.");
                id.Append(faction.ValueRO.FactionId);
                SeedGroupProfiles(em, ecb, entity, geneticProfile, cultureProfile, id);
            }

            ecb.Playback(em);
        }

        private static void SeedGroupProfiles(
            EntityManager em,
            EntityCommandBuffer ecb,
            Entity entity,
            in GeneticProfileDefinition geneticProfile,
            in CultureProfileDefinition cultureProfile,
            in FixedString64Bytes speciesId)
        {
            if (!em.HasComponent<GeneticProfile>(entity))
            {
                ecb.AddComponent(entity, new GeneticProfile
                {
                    Genome = GenomeId.FromString(geneticProfile.Id ?? string.Empty),
                    SpeciesId = speciesId,
                    Generation = 0,
                    Quality01 = 0.55f,
                    Stability01 = 0.55f,
                    Drift01 = 0f,
                    MutationVariance01 = geneticProfile.MutationVariance01,
                    Mutability01 = geneticProfile.Mutability01,
                    AllowedMutationSources = geneticProfile.AllowedMutationSources
                });
            }

            if (!em.HasComponent<GeneticInclination>(entity))
            {
                ecb.AddComponent(entity, new GeneticInclination
                {
                    ViolenceDiplomacyAxis = geneticProfile.ViolenceDiplomacyAxis,
                    MightMagicAxis = geneticProfile.MightMagicAxis
                });
            }

            if (!em.HasComponent<GeneticHabitatPreference>(entity))
            {
                ecb.AddComponent(entity, new GeneticHabitatPreference
                {
                    PrimaryHabitatId = new FixedString32Bytes(geneticProfile.PrimaryHabitatId ?? string.Empty),
                    SecondaryHabitatId = new FixedString32Bytes(geneticProfile.SecondaryHabitatId ?? string.Empty),
                    PreferredBiomeId = new FixedString32Bytes(geneticProfile.PreferredBiomeId ?? string.Empty),
                    PreferredGravity = geneticProfile.PreferredGravity,
                    GravityTolerancePercent = geneticProfile.GravityTolerancePercent,
                    ToleratesExtremeEnvironments = geneticProfile.ToleratesExtremeEnvironments ? (byte)1 : (byte)0
                });
            }

            if (!em.HasComponent<CultureProfile>(entity))
            {
                ecb.AddComponent(entity, new CultureProfile
                {
                    SpiritualMaterialAxis = cultureProfile.SpiritualMaterialAxis,
                    LawfulChaoticAxis = cultureProfile.LawfulChaoticAxis,
                    CorruptPureAxis = cultureProfile.CorruptPureAxis,
                    XenophileXenophobeAxis = cultureProfile.XenophileXenophobeAxis,
                    Mutability01 = cultureProfile.Mutability01,
                    DriftRate01 = cultureProfile.DriftRate01,
                    Cohesion01 = cultureProfile.Cohesion01
                });
            }
        }

        private static GeneticProfileDefinition ResolveGeneticProfile(Space4XGeneticsCatalog catalog, string id)
        {
            if (catalog == null || catalog.GeneticProfiles == null || catalog.GeneticProfiles.Length == 0)
            {
                return default;
            }

            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < catalog.GeneticProfiles.Length; i++)
                {
                    var candidate = catalog.GeneticProfiles[i];
                    if (string.Equals(candidate.Id, id, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return catalog.GeneticProfiles[0];
        }

        private static CultureProfileDefinition ResolveCultureProfile(Space4XGeneticsCatalog catalog, string id)
        {
            if (catalog == null || catalog.CultureProfiles == null || catalog.CultureProfiles.Length == 0)
            {
                return default;
            }

            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < catalog.CultureProfiles.Length; i++)
                {
                    var candidate = catalog.CultureProfiles[i];
                    if (string.Equals(candidate.Id, id, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return catalog.CultureProfiles[0];
        }
    }
}
