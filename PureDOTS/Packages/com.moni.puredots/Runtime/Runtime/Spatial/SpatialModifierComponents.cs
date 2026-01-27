using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Spatial
{
    /// <summary>
    /// Type of spatial modifier.
    /// </summary>
    public enum SpatialModifierType : byte
    {
        // Movement
        MovementSpeed = 0,
        MovementCost = 1,
        
        // Vision
        Visibility = 10,
        SensorRange = 11,
        Stealth = 12,
        
        // Combat
        AccuracyBonus = 20,
        DamageModifier = 21,
        DefenseModifier = 22,
        
        // Resources
        YieldMultiplier = 30,
        GatheringSpeed = 31,
        
        // Time
        TimeFlowRate = 40,
        
        // Health
        PeriodicDamage = 50,
        PeriodicHealing = 51,
        
        // Other
        MoraleModifier = 60,
        TechDiffusion = 61
    }

    /// <summary>
    /// A spatial zone with modifiers.
    /// </summary>
    public struct SpatialZone : IComponentData
    {
        public float3 Center;
        public float Radius;               // Spherical zone
        public float Height;               // For cylinder zones
        public byte ZoneShape;             // 0=sphere, 1=cylinder, 2=box
        public float FalloffStart;         // Where effect starts fading
        public byte IsActive;
    }

    /// <summary>
    /// Modifier applied within a zone.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ZoneModifier : IBufferElementData
    {
        public SpatialModifierType Type;
        public float Value;                // Additive or multiplier
        public byte IsMultiplier;          // 0=additive, 1=multiplier
        public float FalloffCurve;         // How quickly it fades at edges
    }

    /// <summary>
    /// Regional trait (static characteristics).
    /// </summary>
    public struct RegionalTrait : IComponentData
    {
        public FixedString32Bytes TraitName;
        public float BaseMovementMod;
        public float BaseVisibilityMod;
        public float BaseResourceMod;
        public float BaseHazardLevel;
        public byte IsTemporary;
        public uint ExpirationTick;
    }

    /// <summary>
    /// Time flow modifier for region.
    /// </summary>
    public struct TimeFlowRegion : IComponentData
    {
        public float TimeMultiplier;       // 0.5 = half speed, 2.0 = double
        public float StabilityFactor;      // How consistent the flow is
        public float AnomalyChance;        // Chance of temporal anomaly
        public uint LastAnomalyTick;
    }

    /// <summary>
    /// Hazard zone dealing damage.
    /// </summary>
    public struct HazardZone : IComponentData
    {
        public FixedString32Bytes HazardType;
        public float DamagePerTick;
        public float DamageInterval;       // Ticks between damage
        public float ResistanceType;       // What resists this
        public uint LastDamageTick;
    }

    /// <summary>
    /// Accumulated modifiers on an entity from all zones.
    /// </summary>
    public struct AccumulatedSpatialModifiers : IComponentData
    {
        public float MovementMod;          // Final movement multiplier
        public float VisibilityMod;        // Final visibility multiplier
        public float SensorMod;            // Final sensor multiplier
        public float DamageMod;            // Final damage multiplier
        public float TimeFlowMod;          // Final time multiplier
        public float YieldMod;             // Final resource yield multiplier
        public byte InHazard;              // Currently in hazard zone
    }

    /// <summary>
    /// Weather/environmental condition overlay.
    /// </summary>
    public struct WeatherCondition : IComponentData
    {
        public FixedString32Bytes ConditionType;
        public float Intensity;            // 0-1
        public float MovementPenalty;
        public float VisibilityPenalty;
        public float Duration;
        public uint StartTick;
    }
}

