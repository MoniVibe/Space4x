using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Space
{
    /// <summary>
    /// Matches species preferences to planets and calculates compatibility scores.
    /// Burst-compiled system that updates PlanetCompatibility buffers.
    /// Runs in GameplaySystemGroup after PlanetAppealSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    // Removed invalid UpdateAfter: PlanetAppealSystem runs in EnvironmentSystemGroup.
    public partial struct SpeciesPreferenceMatchingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            // Skip if paused or rewinding
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;

            // For each species with preferences, calculate compatibility with all planets
            foreach (var (preferences, preferredFlavors, preferredBiomes, weights, speciesEntity) in SystemAPI.Query<
                RefRO<SpeciesPlanetPreference>,
                DynamicBuffer<PreferredPlanetFlavor>,
                DynamicBuffer<PreferredBiome>,
                RefRO<SpeciesPreferenceWeights>>().WithEntityAccess())
            {
                var speciesPrefs = preferences.ValueRO;
                var speciesWeights = weights.ValueRO;

                // Calculate compatibility for each planet
                foreach (var (planetFlavor, planetBiomes, planetAppeal, planetPhysical, compatibilityBuffer, planetEntity) in SystemAPI.Query<
                    RefRO<PlanetFlavorComponent>,
                    DynamicBuffer<PlanetBiome>,
                    RefRO<PlanetAppeal>,
                    RefRO<PlanetPhysicalProperties>,
                    DynamicBuffer<PlanetCompatibility>>().WithEntityAccess())
                {
                    // Calculate compatibility scores
                    // Use 'in' for read-only buffers to avoid foreach iteration variable issues
                    var flavorMatch = CalculateFlavorMatch(planetFlavor.ValueRO.Flavor, in preferredFlavors);
                    var biomeMatch = CalculateBiomeMatch(in planetBiomes, in preferredBiomes);
                    var appealScore = planetAppeal.ValueRO.AppealScore;
                    var gravityScore = CalculateGravityMatch(planetPhysical.ValueRO.SurfaceGravity, in speciesPrefs);

                    // Weighted average compatibility
                    var totalWeight = speciesWeights.FlavorMatchWeight + speciesWeights.BiomeMatchWeight +
                                     speciesWeights.AppealWeight + speciesWeights.GravityWeight;
                    
                    if (totalWeight <= 0f)
                        totalWeight = 1f; // Avoid division by zero

                    var compatibility = (flavorMatch * speciesWeights.FlavorMatchWeight +
                                        biomeMatch * speciesWeights.BiomeMatchWeight +
                                        appealScore * speciesWeights.AppealWeight +
                                        gravityScore * speciesWeights.GravityWeight) / totalWeight;

                    compatibility = math.clamp(compatibility, 0f, 1f);

                    // Check if planet meets minimum requirements
                    var isHabitable = appealScore >= speciesPrefs.MinAppealThreshold &&
                                     speciesPrefs.IsGravityTolerable(planetPhysical.ValueRO.SurfaceGravity);

                    // Update or add compatibility entry
                    // Get buffer separately to allow passing as ref (foreach iteration variables can't be ref)
                    var compatibilityBufferRW = SystemAPI.GetBuffer<PlanetCompatibility>(planetEntity);
                    UpdateCompatibility(ref compatibilityBufferRW, in speciesEntity, compatibility, flavorMatch, biomeMatch, appealScore, gravityScore, isHabitable, currentTick);
                }
            }
        }

        /// <summary>
        /// Calculate flavor match score based on species preferences.
        /// Uses weighted average if species has multiple preferred flavors.
        /// </summary>
        [BurstCompile]
        private static float CalculateFlavorMatch(PlanetFlavor planetFlavor, in DynamicBuffer<PreferredPlanetFlavor> preferredFlavors)
        {
            if (preferredFlavors.Length == 0)
                return 0.5f; // No preference = neutral score

            var totalWeight = 0f;
            var weightedMatch = 0f;

            for (int i = 0; i < preferredFlavors.Length; i++)
            {
                var pref = preferredFlavors[i];
                var match = (pref.Flavor == planetFlavor) ? 1f : 0f;
                weightedMatch += match * pref.Weight;
                totalWeight += pref.Weight;
            }

            if (totalWeight <= 0f)
                return 0.5f;

            return weightedMatch / totalWeight;
        }

        /// <summary>
        /// Calculate biome match score based on species preferences.
        /// Uses weighted average of biome coverage matching preferred biomes.
        /// </summary>
        [BurstCompile]
        private static float CalculateBiomeMatch(in DynamicBuffer<PlanetBiome> planetBiomes, in DynamicBuffer<PreferredBiome> preferredBiomes)
        {
            if (preferredBiomes.Length == 0)
                return 0.5f; // No preference = neutral score

            if (planetBiomes.Length == 0)
                return 0f; // No biomes = no match

            // Create hash set of preferred biome types for fast lookup
            var preferredSet = new NativeHashSet<int>(preferredBiomes.Length, Allocator.Temp);
            var preferredWeights = new NativeHashMap<int, float>(preferredBiomes.Length, Allocator.Temp);

            for (int i = 0; i < preferredBiomes.Length; i++)
            {
                var pref = preferredBiomes[i];
                preferredSet.Add(pref.BiomeType);
                preferredWeights.TryAdd(pref.BiomeType, pref.Weight);
            }

            // Calculate weighted match based on coverage
            var totalWeight = 0f;
            var weightedMatch = 0f;

            for (int i = 0; i < planetBiomes.Length; i++)
            {
                var biome = planetBiomes[i];
                if (preferredSet.Contains(biome.BiomeType))
                {
                    var weight = preferredWeights[biome.BiomeType];
                    weightedMatch += biome.Coverage * weight;
                    totalWeight += weight;
                }
            }

            preferredSet.Dispose();
            preferredWeights.Dispose();

            if (totalWeight <= 0f)
                return 0.5f;

            // Normalize by total preferred weight
            var maxPossibleWeight = 0f;
            for (int i = 0; i < preferredBiomes.Length; i++)
            {
                maxPossibleWeight += preferredBiomes[i].Weight;
            }

            if (maxPossibleWeight <= 0f)
                return 0.5f;

            return weightedMatch / maxPossibleWeight;
        }

        /// <summary>
        /// Calculate gravity match score based on species tolerance.
        /// </summary>
        [BurstCompile]
        private static float CalculateGravityMatch(float planetGravity, in SpeciesPlanetPreference preferences)
        {
            if (preferences.PreferredGravity <= 0f)
                return 1f; // No preference = perfect match

            if (!preferences.IsGravityTolerable(planetGravity))
                return 0f; // Outside tolerance = no match

            // Score based on how close to preferred gravity
            var difference = math.abs(planetGravity - preferences.PreferredGravity);
            var tolerance = preferences.PreferredGravity * (preferences.GravityTolerancePercent / 100f);
            
            if (tolerance <= 0f)
                return 1f;

            // Linear falloff: 1.0 at preferred, 0.0 at tolerance edge
            var score = 1f - (difference / tolerance);
            return math.clamp(score, 0f, 1f);
        }

        /// <summary>
        /// Update or add compatibility entry in the buffer.
        /// </summary>
        [BurstCompile]
        private static void UpdateCompatibility(
            ref DynamicBuffer<PlanetCompatibility> buffer,
            in Entity speciesEntity,
            float compatibility,
            float flavorMatch,
            float biomeMatch,
            float appealScore,
            float gravityScore,
            bool isHabitable,
            uint currentTick)
        {
            // Find existing entry for this species
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].SpeciesEntity == speciesEntity)
                {
                    // Update existing entry
                    var entry = buffer[i];
                    entry.CompatibilityScore = compatibility;
                    entry.FlavorMatchScore = flavorMatch;
                    entry.BiomeMatchScore = biomeMatch;
                    entry.AppealScore = appealScore;
                    entry.GravityScore = gravityScore;
                    entry.IsHabitable = isHabitable;
                    entry.LastCalculationTick = currentTick;
                    buffer[i] = entry;
                    return;
                }
            }

            // Add new entry
            buffer.Add(new PlanetCompatibility
            {
                SpeciesEntity = speciesEntity,
                CompatibilityScore = compatibility,
                FlavorMatchScore = flavorMatch,
                BiomeMatchScore = biomeMatch,
                AppealScore = appealScore,
                GravityScore = gravityScore,
                IsHabitable = isHabitable,
                LastCalculationTick = currentTick
            });
        }
    }
}

