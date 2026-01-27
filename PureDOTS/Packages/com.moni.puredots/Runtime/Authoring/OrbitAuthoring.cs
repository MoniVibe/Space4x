using PureDOTS.Runtime.Time;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Time
{
    /// <summary>
    /// Authoring component for setting up orbital parameters on planet entities.
    /// Attach to a GameObject representing a planet to enable orbital time-of-day simulation.
    /// </summary>
    public class OrbitAuthoring : MonoBehaviour
    {
        [Tooltip("Orbital period in seconds. How long it takes for the planet to complete one full orbit. Default: 86400 (24 hours).")]
        public float OrbitalPeriodSeconds = 86400f;

        [Tooltip("Initial orbital phase [0..1) at spawn. 0.0 = start of orbit cycle.")]
        [Range(0f, 1f)]
        public float InitialPhase = 0f;

        [Tooltip("Normal vector of the orbital plane (for future 3D position calculations).")]
        public Vector3 OrbitNormal = Vector3.up;

        [Tooltip("Optional offset to apply to orbital phase when computing time-of-day. Allows planets to start at different times of day.")]
        [Range(0f, 1f)]
        public float TimeOfDayOffset = 0f;

        [Header("Time-of-Day Configuration")]
        [Tooltip("Threshold [0..1] marking the start of Dawn phase. Default: 0.0 (midnight).")]
        [Range(0f, 1f)]
        public float DawnThreshold = 0.0f;

        [Tooltip("Threshold [0..1] marking the start of Day phase. Default: 0.25 (6 AM if day = 24 hours).")]
        [Range(0f, 1f)]
        public float DayThreshold = 0.25f;

        [Tooltip("Threshold [0..1] marking the start of Dusk phase. Default: 0.75 (6 PM if day = 24 hours).")]
        [Range(0f, 1f)]
        public float DuskThreshold = 0.75f;

        [Tooltip("Threshold [0..1] marking the start of Night phase. Default: 0.9 (9 PM if day = 24 hours).")]
        [Range(0f, 1f)]
        public float NightThreshold = 0.9f;

        [Tooltip("Minimum sunlight value during night [0..1]. Default: 0.0 (complete darkness).")]
        [Range(0f, 1f)]
        public float MinSunlight = 0.0f;

        [Tooltip("Maximum sunlight value during day [0..1]. Default: 1.0 (full sunlight).")]
        [Range(0f, 1f)]
        public float MaxSunlight = 1.0f;
    }

    /// <summary>
    /// Baker for OrbitAuthoring component.
    /// Converts MonoBehaviour authoring data to ECS components.
    /// </summary>
    public class OrbitBaker : Baker<OrbitAuthoring>
    {
        public override void Bake(OrbitAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add orbit parameters
            AddComponent(entity, new OrbitParameters
            {
                OrbitalPeriodSeconds = authoring.OrbitalPeriodSeconds > 0f 
                    ? authoring.OrbitalPeriodSeconds 
                    : OrbitParameters.Default.OrbitalPeriodSeconds,
                InitialPhase = math.frac(authoring.InitialPhase),
                OrbitNormal = authoring.OrbitNormal.normalized,
                TimeOfDayOffset = math.frac(authoring.TimeOfDayOffset),
                ParentPlanet = Entity.Null // OrbitAuthoring doesn't support parent planets; use PlanetAuthoring for moons
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
                Phase = TimeOfDayPhase.Night, // Will be computed by TimeOfDaySystem
                PreviousPhase = TimeOfDayPhase.Night
            });

            // Add sunlight factor
            AddComponent(entity, new SunlightFactor
            {
                Sunlight = 0f // Will be computed by TimeOfDaySystem
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
        }
    }
}

