using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Parameters defining an orbital system for a planet orbiting a star.
    /// Attach to planet entities to enable orbital time-of-day simulation.
    /// </summary>
    public struct OrbitParameters : IComponentData
    {
        /// <summary>
        /// Orbital period in seconds. How long it takes for the planet to complete one full orbit.
        /// Default: 86400 seconds (24 hours).
        /// </summary>
        public float OrbitalPeriodSeconds;

        /// <summary>
        /// Initial orbital phase [0..1) at spawn/start. 0.0 = start of orbit cycle.
        /// </summary>
        public float InitialPhase;

        /// <summary>
        /// Normal vector of the orbital plane (for future 3D position calculations).
        /// Default: (0, 1, 0) for standard horizontal orbit.
        /// </summary>
        public float3 OrbitNormal;

        /// <summary>
        /// Optional offset to apply to orbital phase when computing time-of-day.
        /// Allows planets to start at different times of day.
        /// </summary>
        public float TimeOfDayOffset;

        /// <summary>
        /// Optional reference to parent planet (for moons/satellites).
        /// Entity.Null means this entity orbits a star directly.
        /// </summary>
        public Entity ParentPlanet;

        /// <summary>
        /// Default constructor with sensible defaults.
        /// </summary>
        public static OrbitParameters Default => new OrbitParameters
        {
            OrbitalPeriodSeconds = 86400f, // 24 hours
            InitialPhase = 0f,
            OrbitNormal = new float3(0f, 1f, 0f),
            TimeOfDayOffset = 0f,
            ParentPlanet = Entity.Null // Orbits star directly
        };
    }

    /// <summary>
    /// Runtime state tracking the current orbital phase of a planet.
    /// Updated by OrbitAdvanceSystem each frame.
    /// </summary>
    public struct OrbitState : IComponentData
    {
        /// <summary>
        /// Current orbital phase [0..1), where 0.0 is the start of the orbit cycle.
        /// Wraps continuously as the planet orbits.
        /// </summary>
        public float OrbitalPhase;

        /// <summary>
        /// Tick when this state was last updated (for diagnostics).
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Normalized time-of-day state derived from orbital phase.
    /// Updated by TimeOfDaySystem.
    /// </summary>
    public struct TimeOfDayState : IComponentData
    {
        /// <summary>
        /// Normalized time-of-day [0..1), where:
        /// 0.0 = midnight (start of day)
        /// 0.25 = dawn
        /// 0.5 = noon
        /// 0.75 = dusk
        /// 1.0 wraps back to midnight
        /// </summary>
        public float TimeOfDayNorm;

        /// <summary>
        /// Current time-of-day phase (Dawn, Day, Dusk, Night).
        /// </summary>
        public TimeOfDayPhase Phase;

        /// <summary>
        /// Previous phase (for detecting phase transitions).
        /// </summary>
        public TimeOfDayPhase PreviousPhase;
    }

    /// <summary>
    /// Enumeration of time-of-day phases.
    /// </summary>
    public enum TimeOfDayPhase : byte
    {
        /// <summary>Dawn phase - transition from night to day.</summary>
        Dawn = 0,
        /// <summary>Day phase - full daylight.</summary>
        Day = 1,
        /// <summary>Dusk phase - transition from day to night.</summary>
        Dusk = 2,
        /// <summary>Night phase - darkness.</summary>
        Night = 3
    }

    /// <summary>
    /// Sunlight factor [0..1] computed from orbital phase.
    /// 0.0 = complete darkness (night)
    /// 1.0 = maximum sunlight (mid-day)
    /// Updated by TimeOfDaySystem, consumed by vegetation and other systems.
    /// </summary>
    public struct SunlightFactor : IComponentData
    {
        /// <summary>
        /// Current sunlight factor [0..1].
        /// </summary>
        public float Sunlight;
    }

    /// <summary>
    /// Configuration for time-of-day phase thresholds and sunlight curve parameters.
    /// Attach to planet entities or use as a singleton for global defaults.
    /// </summary>
    public struct TimeOfDayConfig : IComponentData
    {
        /// <summary>
        /// Threshold [0..1] marking the start of Dawn phase.
        /// Default: 0.0 (midnight).
        /// </summary>
        public float DawnThreshold;

        /// <summary>
        /// Threshold [0..1] marking the start of Day phase.
        /// Default: 0.25 (6 AM if day = 24 hours).
        /// </summary>
        public float DayThreshold;

        /// <summary>
        /// Threshold [0..1] marking the start of Dusk phase.
        /// Default: 0.75 (6 PM if day = 24 hours).
        /// </summary>
        public float DuskThreshold;

        /// <summary>
        /// Threshold [0..1] marking the start of Night phase.
        /// Default: 0.9 (9 PM if day = 24 hours).
        /// </summary>
        public float NightThreshold;

        /// <summary>
        /// Minimum sunlight value during night [0..1].
        /// Default: 0.0 (complete darkness).
        /// </summary>
        public float MinSunlight;

        /// <summary>
        /// Maximum sunlight value during day [0..1].
        /// Default: 1.0 (full sunlight).
        /// </summary>
        public float MaxSunlight;

        /// <summary>
        /// Default constructor with sensible phase thresholds.
        /// </summary>
        public static TimeOfDayConfig Default => new TimeOfDayConfig
        {
            DawnThreshold = 0.0f,
            DayThreshold = 0.25f,
            DuskThreshold = 0.75f,
            NightThreshold = 0.9f,
            MinSunlight = 0.0f,
            MaxSunlight = 1.0f
        };
    }
}

