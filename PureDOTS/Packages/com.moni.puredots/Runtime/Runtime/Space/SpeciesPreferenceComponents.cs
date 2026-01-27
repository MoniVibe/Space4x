using Unity.Entities;
using Unity.Collections;
using System.Runtime.InteropServices;

namespace PureDOTS.Runtime.Space
{
    /// <summary>
    /// Preferred planet flavor for a species.
    /// Stored in a buffer on species entities.
    /// </summary>
    public struct PreferredPlanetFlavor : IBufferElementData
    {
        /// <summary>Planet flavor that this species prefers.</summary>
        public PlanetFlavor Flavor;

        /// <summary>Preference weight [0..1]. Higher = stronger preference.</summary>
        public float Weight;
    }

    /// <summary>
    /// Preferred biome type for a species.
    /// Stored in a buffer on species entities.
    /// </summary>
    public struct PreferredBiome : IBufferElementData
    {
        /// <summary>Biome type identifier that this species prefers.</summary>
        public int BiomeType;

        /// <summary>Preference weight [0..1]. Higher = stronger preference.</summary>
        public float Weight;
    }

    /// <summary>
    /// Planet preference configuration for a species/race.
    /// Defines what types of planets this species prefers and can tolerate.
    /// </summary>
    public struct SpeciesPlanetPreference : IComponentData
    {
        /// <summary>Minimum appeal score [0..1] required for this species to consider a planet habitable.</summary>
        public float MinAppealThreshold;

        /// <summary>Preferred surface gravity (in same units as PlanetPhysicalProperties.SurfaceGravity).</summary>
        public float PreferredGravity;

        /// <summary>Gravity tolerance range (±percentage). Species can tolerate gravity within this range.</summary>
        public float GravityTolerancePercent;

        /// <summary>Whether this species can tolerate extreme environments (toxic, volcanic, etc.).</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool ToleratesExtremeEnvironments;

        /// <summary>
        /// Check if a gravity value is within tolerance range.
        /// </summary>
        public bool IsGravityTolerable(float gravity)
        {
            if (PreferredGravity <= 0f)
                return true; // No preference, accept any

            var tolerance = PreferredGravity * (GravityTolerancePercent / 100f);
            var minGravity = PreferredGravity - tolerance;
            var maxGravity = PreferredGravity + tolerance;
            return gravity >= minGravity && gravity <= maxGravity;
        }
    }

    /// <summary>
    /// Weights for different preference factors when matching species to planets.
    /// Allows customization of how different factors contribute to compatibility score.
    /// </summary>
    public struct SpeciesPreferenceWeights : IComponentData
    {
        /// <summary>Weight for flavor match [0..1]. Higher = flavor match matters more.</summary>
        public float FlavorMatchWeight;

        /// <summary>Weight for biome match [0..1]. Higher = biome match matters more.</summary>
        public float BiomeMatchWeight;

        /// <summary>Weight for appeal score [0..1]. Higher = appeal matters more.</summary>
        public float AppealWeight;

        /// <summary>Weight for gravity match [0..1]. Higher = gravity tolerance matters more.</summary>
        public float GravityWeight;

        /// <summary>Default weights with balanced importance.</summary>
        public static SpeciesPreferenceWeights Default => new SpeciesPreferenceWeights
        {
            FlavorMatchWeight = 0.3f,
            BiomeMatchWeight = 0.2f,
            AppealWeight = 0.3f,
            GravityWeight = 0.2f
        };
    }

    /// <summary>
    /// Compatibility score between a species and a planet.
    /// Stored in a buffer on planet entities, one entry per species.
    /// Calculated by SpeciesPreferenceMatchingSystem.
    /// </summary>
    public struct PlanetCompatibility : IBufferElementData
    {
        /// <summary>Entity reference to the species.</summary>
        public Entity SpeciesEntity;

        /// <summary>Compatibility score [0..1], where 1.0 is perfect match.</summary>
        public float CompatibilityScore;

        /// <summary>Breakdown of compatibility factors (for debugging/UI).</summary>
        public float FlavorMatchScore;
        public float BiomeMatchScore;
        public float AppealScore;
        public float GravityScore;

        /// <summary>Whether this planet meets the species' minimum requirements (appeal threshold, gravity tolerance).</summary>
        public bool IsHabitable;

        /// <summary>Tick when compatibility was last calculated.</summary>
        public uint LastCalculationTick;
    }
}

