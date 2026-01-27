using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Celestial
{
    /// <summary>
    /// Type of celestial body.
    /// </summary>
    public enum CelestialBodyType : byte
    {
        Star = 0,           // Primary light source
        Planet = 1,         // Orbits a star
        Moon = 2,           // Orbits a planet
        Asteroid = 3,       // Small rocky body
        Comet = 4,          // Elliptical orbit, periodic
        Station = 5,        // Artificial, orbiting
        GasGiant = 6,       // Large planet, no surface
        DwarfPlanet = 7     // Small planet
    }

    /// <summary>
    /// Spectral class of stars affecting light color and radiation.
    /// </summary>
    public enum SpectralClass : byte
    {
        O = 0,  // Blue, very hot (>30,000K)
        B = 1,  // Blue-white, hot (10,000-30,000K)
        A = 2,  // White (7,500-10,000K)
        F = 3,  // Yellow-white (6,000-7,500K)
        G = 4,  // Yellow, like our Sun (5,200-6,000K)
        K = 5,  // Orange (3,700-5,200K)
        M = 6,  // Red, cool (<3,700K)
        L = 7,  // Brown dwarf
        T = 8,  // Methane dwarf
        Y = 9   // Ultra-cool brown dwarf
    }

    /// <summary>
    /// Main celestial body component.
    /// </summary>
    public struct CelestialBody : IComponentData
    {
        public CelestialBodyType BodyType;
        public SpectralClass Spectral;          // For stars
        public float Mass;                      // In solar masses or kg
        public float Radius;                    // In km or units
        public float3 Position;                 // Current world position
        public float3 Velocity;                 // Current velocity
        public float RotationPeriod;            // Ticks per rotation
        public float CurrentRotation;           // 0-1 rotation phase
        public FixedString32Bytes BodyName;
    }

    /// <summary>
    /// Orbital state for bodies orbiting another body.
    /// </summary>
    public struct OrbitalState : IComponentData
    {
        public Entity ParentBody;               // What we orbit
        public float SemiMajorAxis;             // Orbital radius (average)
        public float Eccentricity;              // 0 = circle, 0-1 = ellipse
        public float Inclination;               // Orbital tilt in radians
        public float ArgumentOfPeriapsis;       // Orientation of ellipse
        public float LongitudeOfAscendingNode;  // Where orbit crosses reference plane
        public float OrbitalPeriod;             // Ticks per orbit
        public float CurrentPhase;              // 0-1 position in orbit
        public float MeanAnomaly;               // Mean anomaly at epoch
        public uint EpochTick;                  // Reference tick for calculations
    }

    /// <summary>
    /// Spatial pose derived from orbital state for system-map placement.
    /// Separate from time-of-day orbit parameters.
    /// </summary>
    public struct CelestialOrbitPose : IComponentData
    {
        public float3 Position;
        public float3 Forward;
        public float3 Up;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Tag to apply celestial orbit pose to LocalTransform (opt-in for moving entities).
    /// </summary>
    public struct ApplyCelestialPoseToLocalTransform : IComponentData { }

    /// <summary>
    /// Light emission properties for celestial bodies.
    /// </summary>
    public struct CelestialLightSource : IComponentData
    {
        public float Luminosity;                // In solar luminosities
        public float EffectiveTemperature;      // In Kelvin
        public float3 LightColor;               // RGB 0-1
        public float RadiationLevel;            // UV/radiation intensity
        public float LightRadius;               // Max light influence radius
        public byte IsActive;                   // Currently emitting light
    }

    /// <summary>
    /// Atmosphere properties for planets/moons.
    /// </summary>
    public struct CelestialAtmosphere : IComponentData
    {
        public float AtmosphericPressure;       // In atmospheres
        public float AtmosphericDensity;        // Affects light scattering
        public float GreenhouseEffect;          // Temperature modifier
        public float3 AtmosphereColor;          // RGB for rendering
        public byte HasAtmosphere;
        public byte Breathable;                 // For habitability
    }

    /// <summary>
    /// Surface properties for solid bodies.
    /// </summary>
    public struct CelestialSurface : IComponentData
    {
        public float SurfaceTemperature;        // In Kelvin
        public float Albedo;                    // Reflectivity 0-1
        public float SurfaceGravity;            // In m/s²
        public byte HasLiquidWater;
        public byte IsHabitable;
        public byte IsTidallyLocked;            // Same face always toward parent
    }

    /// <summary>
    /// Eclipse state when body is blocked by another.
    /// </summary>
    public struct EclipseState : IComponentData
    {
        public Entity OccludingBody;            // What's blocking light
        public Entity LightSource;              // Light being blocked
        public float EclipseFactor;             // 0 = full eclipse, 1 = no eclipse
        public uint StartTick;
        public uint PredictedEndTick;
        public byte IsPartial;                  // Partial or total
    }

    /// <summary>
    /// Ring system around a planet.
    /// </summary>
    public struct PlanetaryRings : IComponentData
    {
        public float InnerRadius;
        public float OuterRadius;
        public float Density;                   // For shadow casting
        public float3 RingPlaneNormal;          // Orientation
        public byte CastsShadow;
    }

    /// <summary>
    /// Registry singleton for celestial bodies.
    /// </summary>
    public struct CelestialRegistry : IComponentData
    {
        public int BodyCount;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in celestial registry buffer.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct CelestialEntry : IBufferElementData
    {
        public Entity Entity;
        public CelestialBodyType BodyType;
        public float3 Position;
        public float Radius;
        public byte IsLightSource;
        public byte HasOrbit;
    }

    /// <summary>
    /// Day/night cycle state for a location.
    /// </summary>
    public struct DayNightState : IComponentData
    {
        public float TimeOfDay;                 // 0-24 hours
        public float DayProgress;               // 0-1 through current day
        public float SunAltitude;               // -1 to 1, 0 = horizon
        public float SunAzimuth;                // Direction in radians
        public byte IsDaytime;
        public byte IsTwilight;
        public byte IsNight;
    }

    /// <summary>
    /// Configuration for day/night cycle.
    /// </summary>
    public struct DayNightConfig : IComponentData
    {
        public float DayLengthTicks;            // Ticks per full day
        public float DawnStartHour;             // When dawn begins (e.g., 5)
        public float DawnEndHour;               // When dawn ends (e.g., 7)
        public float DuskStartHour;             // When dusk begins (e.g., 18)
        public float DuskEndHour;               // When dusk ends (e.g., 20)
        public float NightAmbientLight;         // Minimum ambient 0-1
        public float DayAmbientLight;           // Maximum ambient 0-1
    }

    /// <summary>
    /// Tidal effects from nearby massive bodies.
    /// </summary>
    public struct TidalInfluence : IComponentData
    {
        public Entity InfluencingBody;
        public float TidalForce;                // Strength of tidal effect
        public float TidalPhase;                // Current tidal phase 0-1
        public float HighTideLevel;
        public float LowTideLevel;
    }
}
