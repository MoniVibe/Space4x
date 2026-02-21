using PureDOTS.Runtime;
using PureDOTS.Runtime.Genetics;
using PureDOTS.Runtime.Individual;
using Unity.Collections;
using Unity.Entities;

using IdentityRaceId = PureDOTS.Runtime.Identity.RaceId;

namespace Space4X.Registry
{
    /// <summary>
    /// Seeds baseline genetic and culture profiles on individuals that lack them.
    /// Data-only: no behavior, mutation, or lineage resolution yet.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XIndividualNormalizationSystem))]
    public partial struct Space4XGeneticsSeedSystem : ISystem
    {
        private EntityQuery _geneticSeedQuery;
        private EntityQuery _cultureSeedQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();

            _geneticSeedQuery = SystemAPI.QueryBuilder()
                .WithAll<SimIndividualTag>()
                .WithNone<GeneticProfile>()
                .Build();

            _cultureSeedQuery = SystemAPI.QueryBuilder()
                .WithAll<SimIndividualTag>()
                .WithNone<CultureProfile>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_geneticSeedQuery.IsEmptyIgnoreFilter && _cultureSeedQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            var catalog = Space4XGeneticsCatalog.LoadOrFallback();
            var geneticProfile = ResolveGeneticProfile(catalog, "genetic.baseline");
            var cultureProfile = ResolveCultureProfile(catalog, "culture.baseline");

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            if (!_geneticSeedQuery.IsEmptyIgnoreFilter)
            {
                using var entities = _geneticSeedQuery.ToEntityArray(state.WorldUpdateAllocator);
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    SeedGenetics(em, ecb, entity, geneticProfile);
                }
            }

            if (!_cultureSeedQuery.IsEmptyIgnoreFilter)
            {
                using var entities = _cultureSeedQuery.ToEntityArray(state.WorldUpdateAllocator);
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    SeedCulture(em, ecb, entity, cultureProfile);
                }
            }

            ecb.Playback(em);
        }

        private static void SeedGenetics(EntityManager em, EntityCommandBuffer ecb, Entity entity, in GeneticProfileDefinition profile)
        {
            if (!em.HasComponent<GeneticProfile>(entity))
            {
                var genomeId = GenomeId.FromString(profile.Id ?? string.Empty);
                var speciesId = ResolveSpeciesId(em, entity, profile);

                ecb.AddComponent(entity, new GeneticProfile
                {
                    Genome = genomeId,
                    SpeciesId = speciesId,
                    Generation = 0,
                    Quality01 = 0.6f,
                    Stability01 = 0.6f,
                    Drift01 = 0f,
                    MutationVariance01 = profile.MutationVariance01,
                    Mutability01 = profile.Mutability01,
                    AllowedMutationSources = profile.AllowedMutationSources
                });
            }

            if (!em.HasComponent<GeneticInclination>(entity))
            {
                ecb.AddComponent(entity, new GeneticInclination
                {
                    ViolenceDiplomacyAxis = profile.ViolenceDiplomacyAxis,
                    MightMagicAxis = profile.MightMagicAxis
                });
            }

            if (!em.HasComponent<GeneticHabitatPreference>(entity))
            {
                ecb.AddComponent(entity, new GeneticHabitatPreference
                {
                    PrimaryHabitatId = new FixedString32Bytes(profile.PrimaryHabitatId ?? string.Empty),
                    SecondaryHabitatId = new FixedString32Bytes(profile.SecondaryHabitatId ?? string.Empty),
                    PreferredBiomeId = new FixedString32Bytes(profile.PreferredBiomeId ?? string.Empty),
                    PreferredGravity = profile.PreferredGravity,
                    GravityTolerancePercent = profile.GravityTolerancePercent,
                    ToleratesExtremeEnvironments = profile.ToleratesExtremeEnvironments ? (byte)1 : (byte)0
                });
            }
        }

        private static void SeedCulture(EntityManager em, EntityCommandBuffer ecb, Entity entity, in CultureProfileDefinition profile)
        {
            if (em.HasComponent<CultureProfile>(entity))
            {
                return;
            }

            ecb.AddComponent(entity, new CultureProfile
            {
                SpiritualMaterialAxis = profile.SpiritualMaterialAxis,
                LawfulChaoticAxis = profile.LawfulChaoticAxis,
                CorruptPureAxis = profile.CorruptPureAxis,
                XenophileXenophobeAxis = profile.XenophileXenophobeAxis,
                Mutability01 = profile.Mutability01,
                DriftRate01 = profile.DriftRate01,
                Cohesion01 = profile.Cohesion01
            });
        }

        private static FixedString64Bytes ResolveSpeciesId(EntityManager em, Entity entity, in GeneticProfileDefinition profile)
        {
            if (em.HasComponent<IdentityRaceId>(entity))
            {
                var raceId = em.GetComponentData<IdentityRaceId>(entity);
                if (!raceId.Value.IsEmpty)
                {
                    return raceId.Value;
                }
            }

            if (em.HasComponent<RaceId>(entity))
            {
                var raceId = em.GetComponentData<RaceId>(entity);
                var value = new FixedString64Bytes("space4x.race.");
                value.Append(raceId.Value);
                return value;
            }

            var fallback = profile.Id ?? string.Empty;
            return new FixedString64Bytes(fallback);
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
