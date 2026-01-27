using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Time;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for planets with all properties.
    /// Combines orbit parameters with planet-specific properties (flavor, biomes, resources, etc.).
    /// </summary>
    public class PlanetAuthoring : MonoBehaviour
    {
        [Header("Planet Type")]
        [Tooltip("Primary planet flavor/type.")]
        public PlanetFlavor Flavor = PlanetFlavor.Continental;

        [Header("Physical Properties")]
        [Tooltip("Planet mass in kilograms (or arbitrary units).")]
        public float Mass = 5.97e24f; // Earth mass

        [Tooltip("Planet density in kg/m³ (or arbitrary units).")]
        public float Density = 5514f; // Earth density

        [Tooltip("Planet radius in meters (or arbitrary units).")]
        public float Radius = 6.371e6f; // Earth radius

        [Header("Gravity")]
        [Tooltip("Gravity falloff exponent (default: 2.0 for inverse square law).")]
        public float GravityFalloffExponent = 2.0f;

        [Tooltip("Maximum distance for gravity effects (0 = infinite).")]
        public float GravityMaxDistance = 0f;

        [Header("Orbit Parameters")]
        [Tooltip("Orbital period in seconds. Default: 86400 (24 hours).")]
        public float OrbitalPeriodSeconds = 86400f;

        [Tooltip("Initial orbital phase [0..1).")]
        [Range(0f, 1f)]
        public float InitialPhase = 0f;

        [Tooltip("Normal vector of the orbital plane.")]
        public Vector3 OrbitNormal = Vector3.up;

        [Tooltip("Time-of-day offset [0..1).")]
        [Range(0f, 1f)]
        public float TimeOfDayOffset = 0f;

        [Header("Parent Planet (for Moons)")]
        [Tooltip("Parent planet entity (leave null to orbit star directly).")]
        public GameObject ParentPlanet;

        [Header("Time-of-Day Configuration")]
        [Tooltip("Dawn threshold [0..1]. Default: 0.0 (midnight).")]
        [Range(0f, 1f)]
        public float DawnThreshold = 0.0f;

        [Tooltip("Day threshold [0..1]. Default: 0.25 (6 AM).")]
        [Range(0f, 1f)]
        public float DayThreshold = 0.25f;

        [Tooltip("Dusk threshold [0..1]. Default: 0.75 (6 PM).")]
        [Range(0f, 1f)]
        public float DuskThreshold = 0.75f;

        [Tooltip("Night threshold [0..1]. Default: 0.9 (9 PM).")]
        [Range(0f, 1f)]
        public float NightThreshold = 0.9f;

        [Tooltip("Minimum sunlight [0..1]. Default: 0.0.")]
        [Range(0f, 1f)]
        public float MinSunlight = 0.0f;

        [Tooltip("Maximum sunlight [0..1]. Default: 1.0.")]
        [Range(0f, 1f)]
        public float MaxSunlight = 1.0f;

        [Header("Biomes")]
        [Tooltip("Biome data (type, coverage). Set in inspector or via script.")]
        public PlanetBiomeData[] Biomes = new PlanetBiomeData[0];

        [Header("Resources")]
        [Tooltip("Resource data (type, amount). Set in inspector or via script.")]
        public PlanetResourceData[] Resources = new PlanetResourceData[0];
    }

    /// <summary>
    /// Serializable biome data for authoring.
    /// </summary>
    [System.Serializable]
    public class PlanetBiomeData
    {
        [Tooltip("Biome type identifier.")]
        public int BiomeType;

        [Tooltip("Coverage percentage [0..1].")]
        [Range(0f, 1f)]
        public float Coverage = 0.1f;
    }

    /// <summary>
    /// Serializable resource data for authoring.
    /// </summary>
    [System.Serializable]
    public class PlanetResourceData
    {
        [Tooltip("Resource type name (will be resolved to Entity).")]
        public string ResourceTypeName;

        [Tooltip("Amount of this resource.")]
        public float Amount = 100f;

        [Tooltip("Maximum amount (for renewable resources).")]
        public float MaxAmount = 100f;

        [Tooltip("Regeneration rate per second (0 for non-renewable).")]
        public float RegenerationRate = 0f;
    }

    /// <summary>
    /// Baker for PlanetAuthoring component.
    /// Creates all planet components including orbit, physical properties, biomes, and resources.
    /// </summary>
    public class PlanetBaker : Baker<PlanetAuthoring>
    {
        public override void Bake(PlanetAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add planet flavor
            AddComponent(entity, new PlanetFlavorComponent
            {
                Flavor = authoring.Flavor
            });

            // Calculate surface gravity from mass and radius
            var surfaceGravity = PlanetPhysicalProperties.CalculateSurfaceGravity(authoring.Mass, authoring.Radius);

            // Add physical properties
            AddComponent(entity, new PlanetPhysicalProperties
            {
                Mass = authoring.Mass,
                Density = authoring.Density,
                Radius = authoring.Radius,
                SurfaceGravity = surfaceGravity
            });

            // Add gravity field
            AddComponent(entity, new PlanetGravityField
            {
                SurfaceGravity = surfaceGravity,
                FalloffExponent = authoring.GravityFalloffExponent,
                MaxDistance = authoring.GravityMaxDistance
            });

            // Add orbit parameters
            var parentPlanetEntity = Entity.Null;
            if (authoring.ParentPlanet != null)
            {
                parentPlanetEntity = GetEntity(authoring.ParentPlanet, TransformUsageFlags.Dynamic);
            }

            AddComponent(entity, new OrbitParameters
            {
                OrbitalPeriodSeconds = authoring.OrbitalPeriodSeconds > 0f
                    ? authoring.OrbitalPeriodSeconds
                    : OrbitParameters.Default.OrbitalPeriodSeconds,
                InitialPhase = math.frac(authoring.InitialPhase),
                OrbitNormal = authoring.OrbitNormal.normalized,
                TimeOfDayOffset = math.frac(authoring.TimeOfDayOffset),
                ParentPlanet = parentPlanetEntity
            });

            // Add initial orbit state
            AddComponent(entity, new OrbitState
            {
                OrbitalPhase = math.frac(authoring.InitialPhase),
                LastUpdateTick = 0
            });

            // Add time-of-day state
            AddComponent(entity, new TimeOfDayState
            {
                TimeOfDayNorm = math.frac(authoring.InitialPhase + authoring.TimeOfDayOffset),
                Phase = TimeOfDayPhase.Night,
                PreviousPhase = TimeOfDayPhase.Night
            });

            // Add sunlight factor
            AddComponent(entity, new SunlightFactor
            {
                Sunlight = 0f
            });

            // Add time-of-day config
            AddComponent(entity, new TimeOfDayConfig
            {
                DawnThreshold = math.frac(authoring.DawnThreshold),
                DayThreshold = math.frac(authoring.DayThreshold),
                DuskThreshold = math.frac(authoring.DuskThreshold),
                NightThreshold = math.frac(authoring.NightThreshold),
                MinSunlight = math.clamp(authoring.MinSunlight, 0f, 1f),
                MaxSunlight = math.clamp(authoring.MaxSunlight, 0f, 1f)
            });

            // Add planet parent (for moons)
            AddComponent(entity, new PlanetParent
            {
                ParentPlanet = parentPlanetEntity
            });

            // Add biomes buffer
            var biomesBuffer = AddBuffer<PlanetBiome>(entity);
            foreach (var biomeData in authoring.Biomes)
            {
                biomesBuffer.Add(new PlanetBiome
                {
                    BiomeType = biomeData.BiomeType,
                    Coverage = math.clamp(biomeData.Coverage, 0f, 1f)
                });
            }

            // Add resources buffer (note: ResourceTypeName needs to be resolved to Entity)
            // For now, we'll add placeholder entries. Games should resolve resource types.
            var resourcesBuffer = AddBuffer<PlanetResource>(entity);
            foreach (var resourceData in authoring.Resources)
            {
                // TODO: Resolve ResourceTypeName to Entity reference
                // For now, use Entity.Null as placeholder
                resourcesBuffer.Add(new PlanetResource
                {
                    ResourceType = Entity.Null, // Games should resolve this
                    Amount = resourceData.Amount,
                    MaxAmount = resourceData.MaxAmount,
                    RegenerationRate = resourceData.RegenerationRate
                });
            }

            // Add planet appeal (will be calculated by PlanetAppealSystem)
            AddComponent(entity, new PlanetAppeal
            {
                AppealScore = 0f,
                BaseAppeal = 0f,
                BiomeDiversityBonus = 0f,
                ResourceRichnessBonus = 0f,
                HabitabilityPenalty = 0f,
                LastCalculationTick = 0
            });

            // Add satellite buffer (will be populated by PlanetOrbitHierarchySystem)
            AddBuffer<PlanetSatellite>(entity);
        }
    }
}

