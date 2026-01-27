using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Space
{
    /// <summary>
    /// Star type enumeration.
    /// Defines the structural type of a star system.
    /// </summary>
    public enum StarType : byte
    {
        /// <summary>Single star system.</summary>
        Single = 0,
        /// <summary>Binary star system (two stars).</summary>
        Binary = 1,
        /// <summary>Trinary star system (three stars).</summary>
        Trinary = 2,
        /// <summary>Black hole.</summary>
        BlackHole = 3,
        /// <summary>Magnetar (highly magnetized neutron star).</summary>
        Magnetar = 4,
        /// <summary>Pulsar (rotating neutron star).</summary>
        Pulsar = 5,
        /// <summary>Neutron star.</summary>
        NeutronStar = 6
    }

    /// <summary>
    /// Stellar classification system (Harvard spectral classification).
    /// Defines the spectral type and luminosity class of a star.
    /// </summary>
    public enum StellarClass : byte
    {
        /// <summary>O-type star - very hot, blue, massive.</summary>
        O = 0,
        /// <summary>B-type star - hot, blue-white.</summary>
        B = 1,
        /// <summary>A-type star - white, hot.</summary>
        A = 2,
        /// <summary>F-type star - white-yellow.</summary>
        F = 3,
        /// <summary>G-type star - yellow (like our Sun).</summary>
        G = 4,
        /// <summary>K-type star - orange.</summary>
        K = 5,
        /// <summary>M-type star - red dwarf, most common.</summary>
        M = 6,
        /// <summary>White dwarf - small, dense remnant.</summary>
        WhiteDwarf = 7,
        /// <summary>Brown dwarf - failed star.</summary>
        BrownDwarf = 8,
        /// <summary>Black hole - collapsed star.</summary>
        BlackHole = 9
    }

    /// <summary>
    /// Component storing the star's type (single, binary, etc.).
    /// </summary>
    public struct StarTypeComponent : IComponentData
    {
        /// <summary>Star type.</summary>
        public StarType Type;
    }

    /// <summary>
    /// Component storing the star's stellar class.
    /// </summary>
    public struct StellarClassComponent : IComponentData
    {
        /// <summary>Stellar class.</summary>
        public StellarClass Class;
    }

    /// <summary>
    /// Physical properties of a star.
    /// Mass, density, radius, and temperature.
    /// Similar structure to PlanetPhysicalProperties for consistency.
    /// </summary>
    public struct StarPhysicalProperties : IComponentData
    {
        /// <summary>Star mass in solar masses (or arbitrary units).</summary>
        public float Mass;

        /// <summary>Star density in kg/m³ (or arbitrary units).</summary>
        public float Density;

        /// <summary>Star radius in solar radii (or arbitrary units).</summary>
        public float Radius;

        /// <summary>Surface temperature in Kelvin (or arbitrary units).</summary>
        public float Temperature;
    }

    /// <summary>
    /// Luminosity of a star.
    /// Can be relative to the Sun (1.0 = Sun's luminosity) or absolute.
    /// </summary>
    public struct StarLuminosity : IComponentData
    {
        /// <summary>Luminosity value (relative to Sun or absolute).</summary>
        public float Luminosity;
    }

    /// <summary>
    /// Calculated solar yield from star luminosity.
    /// Updated by StarSolarYieldSystem based on luminosity and config.
    /// </summary>
    public struct StarSolarYield : IComponentData
    {
        /// <summary>Solar yield [0..1], where 1.0 is maximum yield.</summary>
        public float Yield;

        /// <summary>Tick when yield was last calculated.</summary>
        public uint LastCalculationTick;
    }

    /// <summary>
    /// Cluster membership for a star.
    /// Links stars to star clusters for organization and generation.
    /// </summary>
    public struct StarCluster : IComponentData
    {
        /// <summary>Cluster identifier (can be index or name hash).</summary>
        public int ClusterId;
    }

    /// <summary>
    /// Planet entity orbiting this star.
    /// Stored in a buffer on star entities to track orbiting planets.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct StarPlanet : IBufferElementData
    {
        /// <summary>Entity reference to the planet.</summary>
        public Entity PlanetEntity;
    }

    /// <summary>
    /// Reference to parent star (for planets).
    /// Separate from PlanetParent to distinguish star-orbiting planets from moon-orbiting planets.
    /// Entity.Null means planet doesn't orbit a star (shouldn't happen in normal gameplay).
    /// </summary>
    public struct StarParent : IComponentData
    {
        /// <summary>Entity reference to the parent star.</summary>
        public Entity ParentStar;
    }
}
























