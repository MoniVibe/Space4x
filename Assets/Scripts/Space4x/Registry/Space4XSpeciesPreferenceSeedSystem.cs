using PureDOTS.Runtime;
using PureDOTS.Runtime.Genetics;
using PureDOTS.Runtime.Space;
using PureDOTS.Environment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Seeds species planet preference buffers from genetic habitat preferences.
    /// Data-only: no colonization logic or behavior wiring.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XGeneticsGroupSeedSystem))]
    public partial struct Space4XSpeciesPreferenceSeedSystem : ISystem
    {
        private EntityQuery _seedQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();

            _seedQuery = SystemAPI.QueryBuilder()
                .WithAll<GeneticHabitatPreference>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_seedQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (habitat, entity) in SystemAPI.Query<RefRO<GeneticHabitatPreference>>().WithEntityAccess())
            {
                var habitatPref = habitat.ValueRO;

                var needsPreference = !em.HasComponent<SpeciesPlanetPreference>(entity);
                var hasFlavorBuffer = em.HasBuffer<PreferredPlanetFlavor>(entity);
                var hasBiomeBuffer = em.HasBuffer<PreferredBiome>(entity);
                var hasWeights = em.HasComponent<SpeciesPreferenceWeights>(entity);

                if (needsPreference)
                {
                    var minAppeal = habitatPref.ToleratesExtremeEnvironments != 0 ? 0.2f : 0.35f;
                    var tolerance = math.max(0f, habitatPref.GravityTolerancePercent);

                    ecb.AddComponent(entity, new SpeciesPlanetPreference
                    {
                        MinAppealThreshold = minAppeal,
                        PreferredGravity = math.max(0f, habitatPref.PreferredGravity),
                        GravityTolerancePercent = tolerance,
                        ToleratesExtremeEnvironments = habitatPref.ToleratesExtremeEnvironments != 0
                    });
                }

                if (!hasWeights)
                {
                    ecb.AddComponent(entity, SpeciesPreferenceWeights.Default);
                }

                var flavorBuffer = hasFlavorBuffer
                    ? em.GetBuffer<PreferredPlanetFlavor>(entity)
                    : ecb.AddBuffer<PreferredPlanetFlavor>(entity);

                if (flavorBuffer.Length == 0)
                {
                    var hasPrimary = TryMapPlanetFlavor(habitatPref.PrimaryHabitatId, out var primaryFlavor);
                    var hasSecondary = TryMapPlanetFlavor(habitatPref.SecondaryHabitatId, out var secondaryFlavor);

                    if (hasPrimary && hasSecondary && primaryFlavor == secondaryFlavor)
                    {
                        hasSecondary = false;
                    }

                    if (hasPrimary && hasSecondary)
                    {
                        flavorBuffer.Add(new PreferredPlanetFlavor { Flavor = primaryFlavor, Weight = 0.7f });
                        flavorBuffer.Add(new PreferredPlanetFlavor { Flavor = secondaryFlavor, Weight = 0.3f });
                    }
                    else if (hasPrimary)
                    {
                        flavorBuffer.Add(new PreferredPlanetFlavor { Flavor = primaryFlavor, Weight = 1f });
                    }
                    else if (hasSecondary)
                    {
                        flavorBuffer.Add(new PreferredPlanetFlavor { Flavor = secondaryFlavor, Weight = 1f });
                    }
                }

                var biomeBuffer = hasBiomeBuffer
                    ? em.GetBuffer<PreferredBiome>(entity)
                    : ecb.AddBuffer<PreferredBiome>(entity);

                if (biomeBuffer.Length == 0 && TryMapBiome(habitatPref.PreferredBiomeId, out var biomeType))
                {
                    biomeBuffer.Add(new PreferredBiome { BiomeType = (int)biomeType, Weight = 1f });
                }
            }

            ecb.Playback(em);
        }

        private static bool TryMapPlanetFlavor(in FixedString32Bytes habitatId, out PlanetFlavor flavor)
        {
            flavor = PlanetFlavor.Continental;

            if (habitatId.IsEmpty)
            {
                return false;
            }

            switch (habitatId.ToString())
            {
                case "planet.continental":
                    flavor = PlanetFlavor.Continental;
                    return true;
                case "planet.oceanic":
                    flavor = PlanetFlavor.Oceanic;
                    return true;
                case "planet.tropical":
                    flavor = PlanetFlavor.Tropical;
                    return true;
                case "planet.arid":
                    flavor = PlanetFlavor.Arid;
                    return true;
                case "planet.desert":
                    flavor = PlanetFlavor.Desert;
                    return true;
                case "planet.tundra":
                    flavor = PlanetFlavor.Tundra;
                    return true;
                case "planet.arctic":
                    flavor = PlanetFlavor.Arctic;
                    return true;
                case "planet.volcanic":
                    flavor = PlanetFlavor.Volcanic;
                    return true;
                case "planet.toxic":
                    flavor = PlanetFlavor.Toxic;
                    return true;
                case "planet.barren":
                    flavor = PlanetFlavor.Barren;
                    return true;
                case "planet.tidallylocked":
                    flavor = PlanetFlavor.TidallyLocked;
                    return true;
                case "planet.gasgiant":
                    flavor = PlanetFlavor.GasGiant;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryMapBiome(in FixedString32Bytes biomeId, out BiomeType biome)
        {
            biome = BiomeType.Unknown;

            if (biomeId.IsEmpty)
            {
                return false;
            }

            switch (biomeId.ToString())
            {
                case "biome.forest":
                    biome = BiomeType.Forest;
                    return true;
                case "biome.rainforest":
                    biome = BiomeType.Rainforest;
                    return true;
                case "biome.savanna":
                    biome = BiomeType.Savanna;
                    return true;
                case "biome.desert":
                    biome = BiomeType.Desert;
                    return true;
                case "biome.tundra":
                    biome = BiomeType.Tundra;
                    return true;
                case "biome.taiga":
                    biome = BiomeType.Taiga;
                    return true;
                case "biome.grassland":
                    biome = BiomeType.Grassland;
                    return true;
                case "biome.swamp":
                    biome = BiomeType.Swamp;
                    return true;
                default:
                    return false;
            }
        }
    }
}
