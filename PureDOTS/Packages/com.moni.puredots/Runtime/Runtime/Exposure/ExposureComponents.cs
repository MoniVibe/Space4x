using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Exposure
{
    /// <summary>
    /// Type of exposure being tracked.
    /// </summary>
    public enum ExposureType : byte
    {
        Light = 0,          // Visible light
        Heat = 1,           // Thermal exposure
        Cold = 2,           // Cold exposure
        Radiation = 3,      // Harmful radiation
        Magical = 4,        // Magical energy
        Toxin = 5,          // Poison/pollution
        Pressure = 6        // Atmospheric pressure
    }

    /// <summary>
    /// Light preference category for entities.
    /// </summary>
    public enum LightPreferenceCategory : byte
    {
        Photophilic = 0,    // Loves light (plants, most creatures)
        Photoneutral = 1,   // Tolerates all levels
        Sciophilic = 2,     // Prefers shade
        Scotophilic = 3     // Prefers darkness (cave mushrooms)
    }

    /// <summary>
    /// Entity's preferred light level and tolerance.
    /// </summary>
    public struct LightPreference : IComponentData
    {
        public LightPreferenceCategory Category;
        public float PreferredLevel;            // 0-100 ideal light
        public float ToleranceRange;            // +/- from preferred
        public float MinTolerable;              // Absolute minimum
        public float MaxTolerable;              // Absolute maximum
        public float AdaptationRate;            // How fast adjusts
        public byte CanPhotosynthesize;         // Benefits from light
        public byte TakesLightDamage;           // Damaged by excess light
    }

    /// <summary>
    /// Light tolerance thresholds.
    /// </summary>
    public struct LightTolerance : IComponentData
    {
        public float CurrentTolerance;          // Adapted tolerance level
        public float BaseTolerance;             // Natural tolerance
        public float ToleranceMin;              // Minimum after adaptation
        public float ToleranceMax;              // Maximum after adaptation
        public float StressBuildupRate;         // How fast stress accumulates
        public float StressRecoveryRate;        // How fast recovers
        public float CurrentStress;             // Current light stress 0-1
    }

    /// <summary>
    /// Tracks accumulated exposure over time.
    /// </summary>
    public struct ExposureAccumulator : IComponentData
    {
        public ExposureType TrackedType;
        public float CurrentExposure;           // Current level
        public float AccumulatedDose;           // Total over time
        public float PeakExposure;              // Highest recorded
        public float ExposureDuration;          // Continuous exposure ticks
        public float DecayRate;                 // Natural decay per tick
        public uint LastExposureTick;
        public byte IsCritical;                 // Above safe threshold
    }

    /// <summary>
    /// Effects from over or under exposure.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ExposureEffect : IBufferElementData
    {
        public ExposureType Type;
        public FixedString32Bytes EffectId;
        public float Intensity;                 // Effect strength
        public float Duration;                  // Remaining ticks
        public uint AppliedTick;
        public byte IsPositive;                 // Beneficial effect
        public byte IsActive;
    }

    /// <summary>
    /// Photosynthesis rate for vegetation.
    /// </summary>
    public struct PhotosynthesisRate : IComponentData
    {
        public float BaseRate;                  // Base conversion rate
        public float CurrentRate;               // After modifiers
        public float LightEfficiency;           // Light to energy conversion
        public float OptimalLight;              // Best light level
        public float SaturationPoint;           // Light level beyond which no benefit
        public float CompensationPoint;         // Minimum light for net positive
        public float EnergyProduced;            // Current energy output
    }

    /// <summary>
    /// Temperature exposure state.
    /// </summary>
    public struct TemperatureExposure : IComponentData
    {
        public float CurrentTemperature;        // Ambient temperature
        public float BodyTemperature;           // Internal temperature
        public float OptimalTemperature;        // Preferred temperature
        public float ColdThreshold;             // Below = cold stress
        public float HeatThreshold;             // Above = heat stress
        public float HypothermiaThreshold;      // Dangerous cold
        public float HyperthermiaThreshold;     // Dangerous heat
        public float ThermalResistance;         // Insulation
        public byte IsOverheating;
        public byte IsFreezing;
    }

    /// <summary>
    /// Comfort level from environmental factors.
    /// </summary>
    public struct EnvironmentalComfort : IComponentData
    {
        public float LightComfort;              // 0-1 from light
        public float TemperatureComfort;        // 0-1 from temperature
        public float HumidityComfort;           // 0-1 from humidity
        public float OverallComfort;            // Combined 0-1
        public float ComfortModifier;           // Affects behavior
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Circadian rhythm state.
    /// </summary>
    public struct CircadianRhythm : IComponentData
    {
        public float CycleProgress;             // 0-1 through daily cycle
        public float WakeTime;                  // Hour of day to wake
        public float SleepTime;                 // Hour of day to sleep
        public float EnergyLevel;               // Current energy 0-1
        public float RestQuality;               // Sleep quality modifier
        public float JetLag;                    // Cycle disruption
        public byte IsDiurnal;                  // Active during day
        public byte IsNocturnal;                // Active during night
        public byte IsCrepuscular;              // Active at dawn/dusk
    }

    /// <summary>
    /// Seasonal adaptation state.
    /// </summary>
    public struct SeasonalAdaptation : IComponentData
    {
        public float SpringActivity;            // Activity modifier
        public float SummerActivity;
        public float AutumnActivity;
        public float WinterActivity;
        public float CurrentAdaptation;         // Current season modifier
        public byte Hibernates;                 // Goes dormant in winter
        public byte Estivates;                  // Goes dormant in summer
        public byte IsMigrating;                // Currently migrating
    }

    /// <summary>
    /// Growth modifier from environmental exposure.
    /// </summary>
    public struct GrowthModifier : IComponentData
    {
        public float LightModifier;             // From light exposure
        public float TemperatureModifier;       // From temperature
        public float MoistureModifier;          // From water/humidity
        public float NutrientModifier;          // From soil/food
        public float CombinedModifier;          // Overall growth rate
        public float StressModifier;            // From environmental stress
    }

    /// <summary>
    /// Adaptation to environmental conditions.
    /// </summary>
    public struct EnvironmentalAdaptation : IComponentData
    {
        public float LightAdaptation;           // 0-1 adapted to current light
        public float TemperatureAdaptation;     // 0-1 adapted to temperature
        public float AdaptationSpeed;           // How fast adapts
        public float AdaptationDecay;           // How fast loses adaptation
        public float HardinessLevel;            // Overall environmental resistance
    }

    /// <summary>
    /// Exposure thresholds configuration.
    /// </summary>
    public struct ExposureConfig : IComponentData
    {
        public float SafeLightLevel;
        public float DangerousLightLevel;
        public float SafeRadiationLevel;
        public float DangerousRadiationLevel;
        public float ComfortTemperatureMin;
        public float ComfortTemperatureMax;
        public float EffectTriggerThreshold;
        public float DamageThreshold;
    }

    /// <summary>
    /// Sunburn/light damage accumulation.
    /// </summary>
    public struct LightDamage : IComponentData
    {
        public float DamageAccumulated;
        public float DamageThreshold;           // When effects trigger
        public float HealingRate;               // Recovery per tick
        public float ProtectionLevel;           // Sunscreen/shade equiv
        public byte HasSunburn;
        public byte HasLightSensitivity;
    }

    /// <summary>
    /// Vitamin D style benefit from light exposure.
    /// </summary>
    public struct LightBenefit : IComponentData
    {
        public float BenefitLevel;              // Accumulated benefit
        public float OptimalExposure;           // Ideal daily exposure
        public float CurrentExposure;           // Today's exposure
        public float DeficiencyThreshold;       // When problems start
        public float SaturationPoint;           // Max useful exposure
        public byte IsDeficient;
        public byte IsOptimal;
    }
}

