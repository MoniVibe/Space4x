using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Space
{
    /// <summary>
    /// Calculates planet appeal/desirability scores based on planet properties.
    /// Burst-compiled system that updates PlanetAppeal component.
    /// Runs in EnvironmentSystemGroup to provide appeal data for other systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    public partial struct PlanetAppealSystem : ISystem
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

            // Process all planets
            foreach (var (flavor, biomes, resources, physical, appeal) in SystemAPI.Query<
                RefRO<PlanetFlavorComponent>,
                DynamicBuffer<PlanetBiome>,
                DynamicBuffer<PlanetResource>,
                RefRO<PlanetPhysicalProperties>,
                RefRW<PlanetAppeal>>())
            {
                // Calculate base appeal from flavor
                var baseAppeal = CalculateBaseAppeal(flavor.ValueRO.Flavor);

                // Calculate biome diversity bonus
                var biomeDiversityBonus = CalculateBiomeDiversityBonus(in biomes);

                // Calculate resource richness bonus
                var resourceRichnessBonus = CalculateResourceRichnessBonus(in resources);

                // Calculate habitability penalty (from extreme gravity, etc.)
                var habitabilityPenalty = CalculateHabitabilityPenalty(in physical.ValueRO);

                // Calculate total appeal
                var totalAppeal = baseAppeal + biomeDiversityBonus + resourceRichnessBonus - habitabilityPenalty;
                totalAppeal = math.clamp(totalAppeal, 0f, 1f);

                // Update appeal component
                appeal.ValueRW.AppealScore = totalAppeal;
                appeal.ValueRW.BaseAppeal = baseAppeal;
                appeal.ValueRW.BiomeDiversityBonus = biomeDiversityBonus;
                appeal.ValueRW.ResourceRichnessBonus = resourceRichnessBonus;
                appeal.ValueRW.HabitabilityPenalty = habitabilityPenalty;
                appeal.ValueRW.LastCalculationTick = currentTick;
            }
        }

        /// <summary>
        /// Calculate base appeal from planet flavor.
        /// Different flavors have different base appeal values.
        /// </summary>
        [BurstCompile]
        private static float CalculateBaseAppeal(PlanetFlavor flavor)
        {
            // Base appeal values for different planet flavors
            // Higher values = more desirable
            return flavor switch
            {
                PlanetFlavor.Continental => 0.8f,
                PlanetFlavor.Oceanic => 0.7f,
                PlanetFlavor.Tropical => 0.75f,
                PlanetFlavor.Arid => 0.5f,
                PlanetFlavor.Desert => 0.4f,
                PlanetFlavor.Tundra => 0.45f,
                PlanetFlavor.Arctic => 0.3f,
                PlanetFlavor.Volcanic => 0.2f,
                PlanetFlavor.Toxic => 0.1f,
                PlanetFlavor.Barren => 0.05f,
                PlanetFlavor.TidallyLocked => 0.35f, // Moderate appeal (one side habitable)
                PlanetFlavor.GasGiant => 0.0f, // Not habitable
                _ => 0.5f // Default moderate appeal
            };
        }

        /// <summary>
        /// Calculate bonus from biome diversity.
        /// More diverse biomes = higher appeal.
        /// </summary>
        [BurstCompile]
        private static float CalculateBiomeDiversityBonus(in DynamicBuffer<PlanetBiome> biomes)
        {
            if (biomes.Length == 0)
                return 0f;

            // Count unique biomes
            var uniqueBiomeCount = 0;
            var seenBiomes = new NativeHashSet<int>(biomes.Length, Allocator.Temp);

            for (int i = 0; i < biomes.Length; i++)
            {
                var biome = biomes[i];
                if (biome.Coverage > 0.01f && !seenBiomes.Contains(biome.BiomeType)) // Ignore tiny coverage
                {
                    seenBiomes.Add(biome.BiomeType);
                    uniqueBiomeCount++;
                }
            }

            seenBiomes.Dispose();

            // Bonus: 0.1 per unique biome, capped at 0.3
            return math.min(uniqueBiomeCount * 0.1f, 0.3f);
        }

        /// <summary>
        /// Calculate bonus from resource richness.
        /// More resource types = higher appeal.
        /// </summary>
        [BurstCompile]
        private static float CalculateResourceRichnessBonus(in DynamicBuffer<PlanetResource> resources)
        {
            if (resources.Length == 0)
                return 0f;

            // Count resource types with significant amounts
            var resourceTypeCount = 0;
            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i].Amount > 0.01f) // Ignore tiny amounts
                {
                    resourceTypeCount++;
                }
            }

            // Bonus: 0.05 per resource type, capped at 0.2
            return math.min(resourceTypeCount * 0.05f, 0.2f);
        }

        /// <summary>
        /// Calculate habitability penalty from extreme physical properties.
        /// Very high or very low gravity reduces appeal.
        /// </summary>
        [BurstCompile]
        private static float CalculateHabitabilityPenalty(in PlanetPhysicalProperties physical)
        {
            // Earth-like gravity is around 9.8 m/s²
            // Penalize gravity that's too high (>2x Earth) or too low (<0.5x Earth)
            var earthGravity = 9.8f;
            var gravityRatio = physical.SurfaceGravity / earthGravity;

            var penalty = 0f;

            if (gravityRatio > 2.0f)
            {
                // Very high gravity penalty
                penalty = (gravityRatio - 2.0f) * 0.2f;
            }
            else if (gravityRatio < 0.5f)
            {
                // Very low gravity penalty
                penalty = (0.5f - gravityRatio) * 0.2f;
            }

            // Cap penalty at 0.3
            return math.min(penalty, 0.3f);
        }
    }
}

