using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.SpaceWeather
{
    /// <summary>
    /// Type of space weather event.
    /// </summary>
    public enum SpaceWeatherType : byte
    {
        Clear = 0,              // Normal conditions
        SolarFlare = 1,         // Sudden radiation burst
        RadiationStorm = 2,     // Extended high radiation
        CosmicRayBurst = 3,     // High-energy particles
        MagneticStorm = 4,      // Disrupts electronics/shields
        CoranalMassEjection = 5,// Plasma cloud
        SolarWind = 6,          // Constant particle stream
        GammaRayBurst = 7,      // Extreme radiation (rare)
        NeutronStorm = 8        // Penetrating radiation
    }

    /// <summary>
    /// Severity level of space weather.
    /// </summary>
    public enum WeatherSeverity : byte
    {
        Minor = 0,      // Minimal effects
        Moderate = 1,   // Noticeable effects
        Strong = 2,     // Significant effects
        Severe = 3,     // Dangerous conditions
        Extreme = 4     // Life-threatening
    }

    /// <summary>
    /// Current space weather state.
    /// </summary>
    public struct SpaceWeatherState : IComponentData
    {
        public SpaceWeatherType CurrentWeather;
        public WeatherSeverity Severity;
        public float Intensity;                 // 0-100
        public float RadiationLevel;            // Radiation intensity
        public float MagneticIntensity;         // Magnetic field strength
        public float3 ParticleDirection;        // Direction of particle flow
        public uint StartTick;
        public uint DurationTicks;
        public uint PeakTick;                   // When intensity peaks
        public byte IsRamping;                  // Building up or declining
    }

    /// <summary>
    /// Forecast of upcoming space weather events.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct SpaceWeatherForecast : IBufferElementData
    {
        public SpaceWeatherType PredictedType;
        public WeatherSeverity PredictedSeverity;
        public uint PredictedStartTick;
        public uint PredictedDuration;
        public float Probability;               // Confidence 0-1
        public Entity SourceStar;               // Which star causes it
    }

    /// <summary>
    /// Radiation zone in space.
    /// </summary>
    public struct RadiationZone : IComponentData
    {
        public float3 Center;
        public float InnerRadius;               // Full radiation
        public float OuterRadius;               // Falloff edge
        public float Intensity;                 // Radiation level
        public float ExpansionRate;             // Radius change per tick
        public float MaxRadius;                 // Maximum extent
        public float DecayRate;                 // Intensity decay
        public uint CreatedTick;
        public byte IsExpanding;
        public byte IsPersistent;               // Doesn't decay
    }

    /// <summary>
    /// Per-entity radiation exposure tracking.
    /// </summary>
    public struct RadiationExposure : IComponentData
    {
        public float CurrentDose;               // Current radiation level
        public float AccumulatedDose;           // Total exposure over time
        public float DoseRate;                  // Rate of exposure
        public float Resistance;                // Natural resistance 0-1
        public float ShieldingFactor;           // From equipment/shelter
        public uint LastExposureTick;
        public uint ExposureDuration;           // Continuous exposure time
        public byte IsCritical;                 // Above danger threshold
    }

    /// <summary>
    /// Radiation damage thresholds.
    /// </summary>
    public struct RadiationDamageConfig : IComponentData
    {
        public float SafeThreshold;             // Below = no damage
        public float WarningThreshold;          // Above = effects begin
        public float DangerThreshold;           // Above = damage
        public float LethalThreshold;           // Above = rapid damage
        public float DamagePerDose;             // Health damage per unit
        public float RecoveryRate;              // Dose reduction per tick
        public float AccumulationDecay;         // Long-term dose decay
    }

    /// <summary>
    /// Solar activity cycle state.
    /// </summary>
    public struct SolarActivityCycle : IComponentData
    {
        public Entity StarEntity;
        public float CycleProgress;             // 0-1 through solar cycle
        public float CyclePeriod;               // Ticks per full cycle
        public float BaseActivityLevel;         // Minimum activity
        public float PeakActivityLevel;         // Maximum activity
        public float CurrentActivity;           // Current level
        public float FlareChanceMultiplier;     // Affects event probability
    }

    /// <summary>
    /// Magnetic field affecting entities.
    /// </summary>
    public struct MagneticField : IComponentData
    {
        public float3 FieldDirection;           // Field vector
        public float Strength;                  // Tesla equivalent
        public float Stability;                 // How consistent
        public float ProtectionFactor;          // Reduces radiation
        public byte HasMagnetosphere;           // Planet has magnetic field
    }

    /// <summary>
    /// Coronal mass ejection event.
    /// </summary>
    public struct CoronalMassEjection : IComponentData
    {
        public Entity SourceStar;
        public float3 Direction;                // Ejection direction
        public float Speed;                     // Propagation speed
        public float Width;                     // Angular width
        public float Density;                   // Particle density
        public float3 CurrentPosition;          // Leading edge position
        public uint LaunchTick;
        public byte HasArrived;                 // Reached target area
    }

    /// <summary>
    /// Shielding effectiveness against space weather.
    /// </summary>
    public struct SpaceWeatherShielding : IComponentData
    {
        public float RadiationShielding;        // 0-1 radiation block
        public float MagneticShielding;         // 0-1 magnetic storm block
        public float ParticleShielding;         // 0-1 particle block
        public float ShieldIntegrity;           // Current condition
        public float MaxIntegrity;              // Full strength
        public float DegradeRate;               // Degradation during storms
        public float RechargeRate;              // Recovery rate
    }

    /// <summary>
    /// Electronic/system disruption from space weather.
    /// </summary>
    public struct SystemDisruption : IComponentData
    {
        public float SensorDisruption;          // Affects detection
        public float CommunicationDisruption;   // Affects comms
        public float NavigationDisruption;      // Affects navigation
        public float PowerDisruption;           // Affects power systems
        public uint DisruptionStartTick;
        public uint RecoveryTick;
        public byte IsCritical;
    }

    /// <summary>
    /// Space weather registry for tracking active events.
    /// </summary>
    public struct SpaceWeatherRegistry : IComponentData
    {
        public int ActiveEventCount;
        public int ActiveZoneCount;
        public float GlobalRadiationLevel;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in space weather registry.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct SpaceWeatherEvent : IBufferElementData
    {
        public Entity EventEntity;
        public SpaceWeatherType Type;
        public WeatherSeverity Severity;
        public float3 AffectedArea;
        public float Intensity;
        public uint StartTick;
        public uint EndTick;
        public byte IsActive;
    }

    /// <summary>
    /// Warning signal for incoming space weather.
    /// </summary>
    public struct SpaceWeatherWarning : IComponentData
    {
        public SpaceWeatherType IncomingType;
        public WeatherSeverity ExpectedSeverity;
        public uint ArrivalTick;
        public uint WarningIssuedTick;
        public float EstimatedDuration;
        public byte IsUrgent;
    }
}

