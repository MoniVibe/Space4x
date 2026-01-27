using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Space
{
    /// <summary>
    /// Planet flavor/type enumeration.
    /// Defines the primary characteristics of a planet.
    /// </summary>
    public enum PlanetFlavor : byte
    {
        /// <summary>Continental - temperate, habitable, diverse biomes.</summary>
        Continental = 0,
        /// <summary>Oceanic - mostly water, some landmasses.</summary>
        Oceanic = 1,
        /// <summary>Tropical - hot, humid, lush vegetation.</summary>
        Tropical = 2,
        /// <summary>Arid - dry, desert-like, sparse vegetation.</summary>
        Arid = 3,
        /// <summary>Desert - extremely dry, minimal water.</summary>
        Desert = 4,
        /// <summary>Tundra - cold, frozen ground, sparse life.</summary>
        Tundra = 5,
        /// <summary>Arctic - extremely cold, ice-covered.</summary>
        Arctic = 6,
        /// <summary>Volcanic - active volcanism, high temperature.</summary>
        Volcanic = 7,
        /// <summary>Toxic - poisonous atmosphere, hazardous.</summary>
        Toxic = 8,
        /// <summary>Barren - no atmosphere, lifeless.</summary>
        Barren = 9,
        /// <summary>TidallyLocked - one side always faces star.</summary>
        TidallyLocked = 10,
        /// <summary>GasGiant - large gas planet, no solid surface.</summary>
        GasGiant = 11
    }

    /// <summary>
    /// Component storing the planet's primary flavor/type.
    /// </summary>
    public struct PlanetFlavorComponent : IComponentData
    {
        /// <summary>Primary planet flavor/type.</summary>
        public PlanetFlavor Flavor;
    }

    /// <summary>
    /// Biome data stored in a buffer on planets.
    /// Represents a biome type and its coverage percentage on the planet.
    /// </summary>
    public struct PlanetBiome : IBufferElementData
    {
        /// <summary>Biome type identifier (can be enum or hash).</summary>
        public int BiomeType;

        /// <summary>Coverage percentage [0..1] of this biome on the planet.</summary>
        public float Coverage;
    }

    /// <summary>
    /// Physical properties of a planet.
    /// Mass, density, radius, and derived surface gravity.
    /// </summary>
    public struct PlanetPhysicalProperties : IComponentData
    {
        /// <summary>Planet mass in kilograms (or arbitrary units).</summary>
        public float Mass;

        /// <summary>Planet density in kg/m³ (or arbitrary units).</summary>
        public float Density;

        /// <summary>Planet radius in meters (or arbitrary units).</summary>
        public float Radius;

        /// <summary>Surface gravity in m/s² (or arbitrary units). Calculated from mass and radius.</summary>
        public float SurfaceGravity;

        /// <summary>
        /// Calculate surface gravity from mass and radius.
        /// Formula: g = G * M / r² (simplified, G is constant)
        /// </summary>
        public static float CalculateSurfaceGravity(float mass, float radius)
        {
            if (radius <= 0f)
                return 0f;

            // Simplified gravity calculation: g = mass / (radius * radius)
            // In real physics: g = G * M / r², but we use simplified units
            return mass / (radius * radius);
        }
    }

    /// <summary>
    /// Gravity field properties for a planet.
    /// Defines gravity strength at surface and falloff parameters.
    /// </summary>
    public struct PlanetGravityField : IComponentData
    {
        /// <summary>Gravity strength at surface (same as SurfaceGravity from PhysicalProperties).</summary>
        public float SurfaceGravity;

        /// <summary>Falloff exponent for gravity with distance (default: 2.0 for inverse square law).</summary>
        public float FalloffExponent;

        /// <summary>Maximum distance at which gravity affects entities (0 = infinite).</summary>
        public float MaxDistance;

        /// <summary>
        /// Calculate gravity at a given distance from planet center.
        /// </summary>
        public float CalculateGravityAtDistance(float distance)
        {
            if (distance <= 0f || MaxDistance > 0f && distance > MaxDistance)
                return 0f;

            // Inverse square law: g(r) = g_surface * (r_surface / r)^falloff
            // Simplified: g(r) = g_surface / (r / r_surface)^falloff
            var ratio = 1f / math.max(distance, 0.001f); // Avoid division by zero
            return SurfaceGravity * math.pow(ratio, FalloffExponent);
        }
    }

    /// <summary>
    /// Resource available on a planet.
    /// Stored in a buffer on planet entities.
    /// </summary>
    public struct PlanetResource : IBufferElementData
    {
        /// <summary>Resource type identifier (Entity reference or hash).</summary>
        public Entity ResourceType;

        /// <summary>Amount of this resource available on the planet.</summary>
        public float Amount;

        /// <summary>Maximum amount this resource can reach (for renewable resources).</summary>
        public float MaxAmount;

        /// <summary>Regeneration rate per second (0 for non-renewable).</summary>
        public float RegenerationRate;
    }

    /// <summary>
    /// Calculated appeal/desirability score for a planet.
    /// Updated by PlanetAppealSystem based on planet properties.
    /// </summary>
    public struct PlanetAppeal : IComponentData
    {
        /// <summary>Overall appeal score [0..1], where 1.0 is most desirable.</summary>
        public float AppealScore;

        /// <summary>Base appeal from planet flavor.</summary>
        public float BaseAppeal;

        /// <summary>Bonus from biome diversity.</summary>
        public float BiomeDiversityBonus;

        /// <summary>Bonus from resource richness.</summary>
        public float ResourceRichnessBonus;

        /// <summary>Penalty from habitability factors (gravity, temperature extremes, etc.).</summary>
        public float HabitabilityPenalty;

        /// <summary>Tick when appeal was last calculated.</summary>
        public uint LastCalculationTick;
    }

    /// <summary>
    /// Reference to parent planet (for moons/satellites).
    /// Entity.Null means this planet orbits a star directly.
    /// </summary>
    public struct PlanetParent : IComponentData
    {
        /// <summary>Entity reference to the parent planet (or Entity.Null for star-orbiting planets).</summary>
        public Entity ParentPlanet;
    }

    /// <summary>
    /// Satellite entity orbiting this planet.
    /// Stored in a buffer on planet entities to track moons.
    /// </summary>
    public struct PlanetSatellite : IBufferElementData
    {
        /// <summary>Entity reference to the satellite/moon.</summary>
        public Entity SatelliteEntity;
    }
}

