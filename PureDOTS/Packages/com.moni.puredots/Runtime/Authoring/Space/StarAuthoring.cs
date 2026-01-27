using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Time;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Space
{
    /// <summary>
    /// Authoring component for stars with all properties.
    /// Combines orbit parameters (for galactic center orbit) with star-specific properties.
    /// </summary>
    public class StarAuthoring : MonoBehaviour
    {
        [Header("Star Type")]
        [Tooltip("Structural type of the star system.")]
        public StarType Type = StarType.Single;

        [Tooltip("Stellar classification (O, B, A, F, G, K, M, etc.).")]
        public StellarClass StellarClass = StellarClass.G;

        [Header("Physical Properties")]
        [Tooltip("Star mass in solar masses (or arbitrary units).")]
        public float Mass = 1.0f; // Sun mass

        [Tooltip("Star density in kg/m³ (or arbitrary units).")]
        public float Density = 1408f; // Sun density

        [Tooltip("Star radius in solar radii (or arbitrary units).")]
        public float Radius = 1.0f; // Sun radius

        [Tooltip("Surface temperature in Kelvin (or arbitrary units).")]
        public float Temperature = 5778f; // Sun temperature

        [Header("Luminosity")]
        [Tooltip("Luminosity relative to the Sun (1.0 = Sun's luminosity).")]
        public float Luminosity = 1.0f;

        [Header("Cluster")]
        [Tooltip("Cluster identifier (for organization and generation).")]
        public int ClusterId = 0;

        [Header("Galactic Orbit Parameters")]
        [Tooltip("Orbital period around galactic center in seconds. Default: very long (e.g., 2.5e17 for 250 million years).")]
        public float OrbitalPeriodSeconds = 2.5e17f; // ~250 million years

        [Tooltip("Initial orbital phase [0..1).")]
        [Range(0f, 1f)]
        public float InitialPhase = 0f;

        [Tooltip("Normal vector of the orbital plane.")]
        public Vector3 OrbitNormal = Vector3.up;

        /// <summary>
        /// Baker for StarAuthoring component.
        /// Creates all star components including orbit, physical properties, luminosity, and cluster.
        /// </summary>
        public class StarBaker : Baker<StarAuthoring>
        {
            public override void Bake(StarAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add star type
                AddComponent(entity, new StarTypeComponent
                {
                    Type = authoring.Type
                });

                // Add stellar class
                AddComponent(entity, new StellarClassComponent
                {
                    Class = authoring.StellarClass
                });

                // Add physical properties
                AddComponent(entity, new StarPhysicalProperties
                {
                    Mass = authoring.Mass,
                    Density = authoring.Density,
                    Radius = authoring.Radius,
                    Temperature = authoring.Temperature
                });

                // Add luminosity
                AddComponent(entity, new StarLuminosity
                {
                    Luminosity = math.max(0f, authoring.Luminosity)
                });

                // Add solar yield (will be calculated by StarSolarYieldSystem)
                AddComponent(entity, new StarSolarYield
                {
                    Yield = 0f,
                    LastCalculationTick = 0
                });

                // Add cluster
                AddComponent(entity, new StarCluster
                {
                    ClusterId = authoring.ClusterId
                });

                // Add orbit parameters for galactic center orbit
                AddComponent(entity, new OrbitParameters
                {
                    OrbitalPeriodSeconds = authoring.OrbitalPeriodSeconds > 0f
                        ? authoring.OrbitalPeriodSeconds
                        : OrbitParameters.Default.OrbitalPeriodSeconds,
                    InitialPhase = math.frac(authoring.InitialPhase),
                    OrbitNormal = authoring.OrbitNormal.normalized,
                    TimeOfDayOffset = 0f, // Not used for stars
                    ParentPlanet = Entity.Null // Stars orbit galactic center (no parent)
                });

                // Add initial orbit state
                AddComponent(entity, new OrbitState
                {
                    OrbitalPhase = math.frac(authoring.InitialPhase),
                    LastUpdateTick = 0
                });

                // Add star planets buffer (will be populated by PlanetOrbitHierarchySystem)
                AddBuffer<StarPlanet>(entity);
            }
        }
    }
}
























