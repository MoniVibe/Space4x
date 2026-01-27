using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Miracles
{
    /// <summary>
    /// Miracle identifier enum (ushort for more IDs than byte).
    /// </summary>
    public enum MiracleId : ushort
    {
        None = 0,
        Rain = 1,
        TemporalVeil = 2,
        Fire = 3,
        Heal = 4,
        // Future: Verdant, Earthquake, etc.
    }
    
    /// <summary>
    /// Dispensation mode for miracles (how they are cast).
    /// </summary>
    public enum DispenseMode : byte
    {
        Sustained = 1,
        Throw = 2
    }
    
    /// <summary>
    /// Targeting mode for miracles.
    /// </summary>
    public enum TargetingMode : byte
    {
        Point = 0,
        Area = 1,
        Actor = 2,
        Self = 3,
        GroundOnly = 4,
        FriendlyOnly = 5,
        EnemyOnly = 6
    }
    
    /// <summary>
    /// Miracle category for organization and filtering.
    /// </summary>
    public enum MiracleCategory : byte
    {
        Weather = 0,
        Offensive = 1,
        Support = 2,
        Control = 3,
        Epic = 4
    }

    /// <summary>
    /// Delivery archetype for how a miracle manifests after activation.
    /// </summary>
    public enum MiracleDeliveryType : byte
    {
        Instant = 0,
        Projectile = 1,
        SustainedArea = 2,
        Beacon = 3,
        Chain = 4
    }

    /// <summary>
    /// Charging behavior that determines how intensity/radius scale over time.
    /// </summary>
    public enum MiracleChargeModel : byte
    {
        None = 0,
        HoldToTier = 1,
        HoldToContinuous = 2
    }
    
    /// <summary>
    /// Easing used by miracles that charge continuously over time.
    /// </summary>
    public enum MiracleChargeCurveType : byte
    {
        Linear = 0,
        EaseIn = 1,
        EaseOut = 2,
        EaseInOut = 3
    }
    
    /// <summary>
    /// Specification for a miracle type in the catalog.
    /// </summary>
    public struct MiracleSpec
    {
        public MiracleId Id;
        public float BaseCooldownSeconds;
        public float BasePrayerCost;   // Not enforced in MVP, but present
        public float BaseRadius;
        public float MaxRadius;
        public byte MaxCharges;        // 1 for MVP
        public byte Tier;              // 1=small, 2=medium, 3=epic
        public byte AllowedDispenseModes; // bitmask: Sustained, Throw
        public TargetingMode TargetingMode;
        public MiracleCategory Category;
        public MiracleDeliveryType DeliveryType;
        public MiracleChargeModel ChargeModel;
        public byte TierCount;             // Number of progressive charge tiers (min 1)
        public BlobArray<float> TierTimeThresholds; // Seconds required per tier (HoldToTier)
        public float ChargeTimeMax;         // Max seconds to hit full charge (HoldToContinuous)
        public float RadiusChargeMultiplier;// 1.0 = no change, 2.0 = double radius at max
        public float StrengthChargeMultiplier;// Intensity multiplier at max charge
        public MiracleChargeCurveType ChargeCurveType; // Curve for HoldToContinuous
        public float BaseDuration;         // Base lifetime seconds (0 = instant/persistent)
        public float BaseStrength;         // Base intensity multiplier
        public float CostUpfront;          // Explicit upfront cost (mirrors BasePrayerCost for now)
        public float CostPerSecond;        // Sustained drain per second
        
        // Throw mechanics (for Throw dispense mode)
        public float ThrowSpeedBase;        // Base throw speed (m/s)
        public float ThrowSpeedChargeMultiplier; // Speed multiplier at max charge (1.0 = no change)
        public float ThrowArcBoost;         // Upward arc component (0 = flat, >0 = arc)
        public float ThrowCollisionRadius;  // Collider radius for token (default 0.5f)
    }
    
    /// <summary>
    /// Catalog blob containing all miracle specifications.
    /// </summary>
    public struct MiracleCatalogBlob
    {
        public BlobArray<MiracleSpec> Specs;
    }
    
    /// <summary>
    /// Singleton component providing access to the miracle catalog.
    /// </summary>
    public struct MiracleConfigState : IComponentData
    {
        public BlobAssetReference<MiracleCatalogBlob> Catalog;
        public float GlobalCooldownScale; // For tuning/difficulty
    }
}
























