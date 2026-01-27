#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Miracles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Miracles
{
    /// <summary>
    /// ScriptableObject asset for miracle catalog data.
    /// </summary>
    [CreateAssetMenu(fileName = "MiracleCatalog", menuName = "PureDOTS/Miracles/Miracle Catalog", order = 100)]
    public sealed class MiracleCatalogAsset : ScriptableObject
    {
        [Serializable]
        public class MiracleSpecData
        {
            [Header("Identity")]
            public MiracleId id = MiracleId.None;
            
            [Header("Timing")]
            [Min(0f)]
            public float baseCooldownSeconds = 10f;
            [Min(0f)]
            public float basePrayerCost = 0f; // Not enforced in MVP
            
            [Header("Area")]
            [Min(0f)]
            public float baseRadius = 10f;
            [Min(0f)]
            public float maxRadius = 50f;
            
            [Header("Charges")]
            [Range(1, 10)]
            public byte maxCharges = 1;
            
            [Header("Properties")]
            [Range(1, 3)]
            public byte tier = 1; // 1=small, 2=medium, 3=epic
            
            [Header("Dispensation")]
            [Tooltip("Bitmask: 1=Sustained, 2=Throw")]
            public byte allowedDispenseModes = 3; // Both by default
            
            [Header("Targeting")]
            public TargetingMode targetingMode = TargetingMode.Point;
            
            [Header("Category")]
            public MiracleCategory category = MiracleCategory.Weather;

            [Header("Delivery")]
            public MiracleDeliveryType deliveryType = MiracleDeliveryType.Instant;

            [Header("Charging")]
            public MiracleChargeModel chargeModel = MiracleChargeModel.None;
            [Range(1, 5)]
            public byte tierCount = 1;
            [Tooltip("Seconds required to reach each tier (HoldToTier). Length should match Tier Count.")]
            public List<float> tierTimeThresholds = new();
            [Tooltip("Seconds required to reach full charge (HoldToContinuous).")]
            [Min(0f)]
            public float chargeTimeMax = 1f;
            [Tooltip("Radius multiplier applied at max charge (1 = no change).")]
            public float radiusChargeMultiplier = 1f;
            [Tooltip("Strength/Intensity multiplier applied at max charge (1 = no change).")]
            public float strengthChargeMultiplier = 1f;
            [Tooltip("Curve used when mapping HoldToContinuous charge time to Charge01.")]
            public MiracleChargeCurveType chargeCurveType = MiracleChargeCurveType.Linear;

            [Header("Effect Parameters")]
            [Tooltip("Base duration in seconds. 0 means instant or persistent depending on the delivery type.")]
            [Min(0f)]
            public float baseDuration = 0f;
            [Tooltip("Base intensity multiplier applied before charge scaling.")]
            [Min(0f)]
            public float baseStrength = 1f;

            [Header("Cost")]
            [Tooltip("Upfront resource cost; defaults to Base Prayer Cost when left at 0.")]
            [Min(0f)]
            public float costUpfront = 0f;
            [Tooltip("Per-second drain while channeling sustained miracles.")]
            [Min(0f)]
            public float costPerSecond = 0f;

            [Header("Throw Mechanics")]
            [Tooltip("Base throw speed in m/s (for Throw dispense mode).")]
            [Min(0f)]
            public float throwSpeedBase = 20f;
            [Tooltip("Speed multiplier at max charge (1.0 = no change, 2.0 = double speed).")]
            [Min(0f)]
            public float throwSpeedChargeMultiplier = 1.5f;
            [Tooltip("Upward arc boost (0 = flat trajectory, >0 = arc).")]
            [Min(0f)]
            public float throwArcBoost = 5f;
            [Tooltip("Collision radius for throw token (for impact detection).")]
            [Min(0.1f)]
            public float throwCollisionRadius = 0.5f;
        }
        
        public List<MiracleSpecData> specs = new();
    }
    
    /// <summary>
    /// MonoBehaviour authoring component that references a miracle catalog asset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiracleCatalogAuthoring : MonoBehaviour
    {
        [Tooltip("Reference to the ScriptableObject catalog that defines miracle data.")]
        public MiracleCatalogAsset catalog;
        
        [Tooltip("Global cooldown scale multiplier (for difficulty tuning).")]
        [Min(0.1f)]
        public float globalCooldownScale = 1f;
        
        class Baker : Unity.Entities.Baker<MiracleCatalogAuthoring>
        {
            public override void Bake(MiracleCatalogAuthoring authoring)
            {
                if (authoring.catalog == null)
                {
                    Debug.LogWarning("[MiracleCatalogBaker] Missing catalog reference.");
                    return;
                }
                
                var catalog = authoring.catalog;
                if (catalog.specs == null || catalog.specs.Count == 0)
                {
                    Debug.LogWarning($"[MiracleCatalogBaker] No specs defined in {catalog.name}.");
                    return;
                }
                
                // Build blob data
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<MiracleCatalogBlob>();
                var specsArray = builder.Allocate(ref catalogBlob.Specs, catalog.specs.Count);
                
                for (int i = 0; i < catalog.specs.Count; i++)
                {
                    var src = catalog.specs[i];

                    byte tierCount = src.tierCount;
                    if (tierCount < 1)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] Tier count < 1 for {src.id}; defaulting to 1.", catalog);
                        tierCount = 1;
                    }

                    float baseDuration = math.max(0f, src.baseDuration);
                    if (src.baseDuration < 0f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] Base duration < 0 for {src.id}; clamping to 0.", catalog);
                    }

                    float baseStrength = src.baseStrength;
                    if (baseStrength <= 0f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] Base strength <= 0 for {src.id}; defaulting to 1.", catalog);
                        baseStrength = 1f;
                    }

                    float costUpfront = src.costUpfront > 0f ? src.costUpfront : src.basePrayerCost;
                    if (costUpfront < 0f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] Cost upfront < 0 for {src.id}; clamping to 0.", catalog);
                        costUpfront = 0f;
                    }

                    float costPerSecond = math.max(0f, src.costPerSecond);
                    if (src.costPerSecond < 0f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] Cost per second < 0 for {src.id}; clamping to 0.", catalog);
                    }

                    float chargeTimeMax = src.chargeTimeMax;
                    if (chargeTimeMax <= 0f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] ChargeTimeMax <= 0 for {src.id}; defaulting to 1 second.", catalog);
                        chargeTimeMax = 1f;
                    }

                    float radiusChargeMultiplier = src.radiusChargeMultiplier;
                    if (radiusChargeMultiplier <= 0f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] RadiusChargeMultiplier <= 0 for {src.id}; defaulting to 1.", catalog);
                        radiusChargeMultiplier = 1f;
                    }

                    float strengthChargeMultiplier = src.strengthChargeMultiplier;
                    if (strengthChargeMultiplier <= 0f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] StrengthChargeMultiplier <= 0 for {src.id}; defaulting to 1.", catalog);
                        strengthChargeMultiplier = 1f;
                    }

                    float throwSpeedBase = math.max(0.1f, src.throwSpeedBase);
                    if (src.throwSpeedBase <= 0f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] ThrowSpeedBase <= 0 for {src.id}; defaulting to 20.", catalog);
                        throwSpeedBase = 20f;
                    }

                    float throwSpeedChargeMultiplier = math.max(0f, src.throwSpeedChargeMultiplier);
                    if (src.throwSpeedChargeMultiplier <= 0f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] ThrowSpeedChargeMultiplier <= 0 for {src.id}; defaulting to 1.5.", catalog);
                        throwSpeedChargeMultiplier = 1.5f;
                    }

                    float throwArcBoost = math.max(0f, src.throwArcBoost);
                    if (src.throwArcBoost < 0f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] ThrowArcBoost < 0 for {src.id}; clamping to 0.", catalog);
                    }

                    float throwCollisionRadius = math.max(0.1f, src.throwCollisionRadius);
                    if (src.throwCollisionRadius < 0.1f)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] ThrowCollisionRadius < 0.1 for {src.id}; clamping to 0.5.", catalog);
                        throwCollisionRadius = 0.5f;
                    }

                    ref var dest = ref specsArray[i];
                    dest.Id = src.id;
                    dest.BaseCooldownSeconds = math.max(0f, src.baseCooldownSeconds);
                    dest.BasePrayerCost = math.max(0f, src.basePrayerCost);
                    dest.BaseRadius = math.max(0f, src.baseRadius);
                    dest.MaxRadius = math.max(src.baseRadius, src.maxRadius);
                    dest.MaxCharges = (byte)math.clamp(src.maxCharges, 1, 10);
                    dest.Tier = (byte)math.clamp(src.tier, 1, 3);
                    dest.AllowedDispenseModes = src.allowedDispenseModes;
                    dest.TargetingMode = src.targetingMode;
                    dest.Category = src.category;
                    dest.DeliveryType = src.deliveryType;
                    dest.ChargeModel = src.chargeModel;
                    dest.TierCount = tierCount;
                    dest.ChargeTimeMax = chargeTimeMax;
                    dest.RadiusChargeMultiplier = radiusChargeMultiplier;
                    dest.StrengthChargeMultiplier = strengthChargeMultiplier;
                    dest.ChargeCurveType = src.chargeCurveType;
                    dest.BaseDuration = baseDuration;
                    dest.BaseStrength = baseStrength;
                    dest.CostUpfront = costUpfront;
                    dest.CostPerSecond = costPerSecond;
                    dest.ThrowSpeedBase = throwSpeedBase;
                    dest.ThrowSpeedChargeMultiplier = throwSpeedChargeMultiplier;
                    dest.ThrowArcBoost = throwArcBoost;
                    dest.ThrowCollisionRadius = throwCollisionRadius;

                    int thresholdsTarget = tierCount;
                    var thresholdList = src.tierTimeThresholds ?? new List<float>();
                    if (thresholdList.Count < thresholdsTarget && src.chargeModel == MiracleChargeModel.HoldToTier)
                    {
                        Debug.LogWarning($"[MiracleCatalogBaker] Tier thresholds missing for {src.id}; padding with 0.5s steps.", catalog);
                    }

                    var thresholds = builder.Allocate(ref dest.TierTimeThresholds, thresholdsTarget);
                    float lastValue = 0f;
                    for (int t = 0; t < thresholdsTarget; t++)
                    {
                        float value = t < thresholdList.Count ? thresholdList[t] : (lastValue + 0.5f);
                        value = math.max(0f, value);
                        if (value < lastValue)
                        {
                            Debug.LogWarning($"[MiracleCatalogBaker] Tier threshold {t} < previous for {src.id}; clamping.", catalog);
                            value = lastValue;
                        }

                        thresholds[t] = value;
                        lastValue = value;
                    }
                }
                
                var blobAsset = builder.CreateBlobAssetReference<MiracleCatalogBlob>(Allocator.Persistent);
                var entity = GetEntity(TransformUsageFlags.None);
                AddBlobAsset(ref blobAsset, out _);
                AddComponent(entity, new MiracleConfigState
                {
                    Catalog = blobAsset,
                    GlobalCooldownScale = math.max(0.1f, authoring.globalCooldownScale)
                });
            }
        }
    }
}
#endif
























