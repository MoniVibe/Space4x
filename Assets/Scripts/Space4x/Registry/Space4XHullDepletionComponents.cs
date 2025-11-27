using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Hull integrity tracking with permanent damage support.
    /// </summary>
    public struct HullIntegrity : IComponentData
    {
        /// <summary>
        /// Current hull points.
        /// </summary>
        public float Current;

        /// <summary>
        /// Maximum hull points (can be reduced by permanent damage).
        /// </summary>
        public float Max;

        /// <summary>
        /// Base maximum hull points (design spec, never changes).
        /// </summary>
        public float BaseMax;

        /// <summary>
        /// Armor rating (damage reduction).
        /// </summary>
        public half ArmorRating;

        /// <summary>
        /// Last tick when hull was damaged.
        /// </summary>
        public uint LastDamageTick;

        /// <summary>
        /// Last tick when hull was repaired.
        /// </summary>
        public uint LastRepairTick;

        /// <summary>
        /// Current hull ratio [0, 1].
        /// </summary>
        public float Ratio => Max > 0 ? Current / Max : 0f;

        /// <summary>
        /// Permanent damage ratio [0, 1]. How much BaseMax has been lost.
        /// </summary>
        public float PermanentDamageRatio => BaseMax > 0 ? 1f - (Max / BaseMax) : 0f;

        /// <summary>
        /// Whether hull has permanent damage requiring dockyard repair.
        /// </summary>
        public bool HasPermanentDamage => Max < BaseMax;

        public static HullIntegrity Create(float baseMax, float armorRating = 0f)
        {
            return new HullIntegrity
            {
                Current = baseMax,
                Max = baseMax,
                BaseMax = baseMax,
                ArmorRating = (half)math.clamp(armorRating, 0f, 0.9f),
                LastDamageTick = 0,
                LastRepairTick = 0
            };
        }

        public static HullIntegrity LightCraft => Create(50f, 0.1f);
        public static HullIntegrity MediumCraft => Create(150f, 0.2f);
        public static HullIntegrity HeavyCraft => Create(300f, 0.3f);
        public static HullIntegrity LightCarrier => Create(800f, 0.25f);
        public static HullIntegrity HeavyCarrier => Create(1500f, 0.35f);
        public static HullIntegrity SuperCarrier => Create(3000f, 0.4f);
    }

    /// <summary>
    /// Types of critical damage.
    /// </summary>
    public enum CriticalDamageType : byte
    {
        None = 0,
        HullBreach = 1,
        ReactorDamage = 2,
        EngineFailure = 3,
        WeaponMalfunction = 4,
        SensorBlindness = 5,
        LifeSupportFailure = 6,
        BridgeHit = 7,
        AmmoCookoff = 8
    }

    /// <summary>
    /// Record of critical damage received.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CriticalDamageHistory : IBufferElementData
    {
        /// <summary>
        /// Type of critical damage.
        /// </summary>
        public CriticalDamageType Type;

        /// <summary>
        /// Severity [0, 1]. Higher = more severe.
        /// </summary>
        public half Severity;

        /// <summary>
        /// Tick when damage was received.
        /// </summary>
        public uint DamageTick;

        /// <summary>
        /// Whether damage has been repaired.
        /// </summary>
        public byte Repaired;

        /// <summary>
        /// Permanent max hull reduction caused by this damage.
        /// </summary>
        public float MaxHullReduction;

        public static CriticalDamageHistory Create(CriticalDamageType type, float severity, float maxHullReduction, uint tick)
        {
            return new CriticalDamageHistory
            {
                Type = type,
                Severity = (half)math.clamp(severity, 0f, 1f),
                DamageTick = tick,
                Repaired = 0,
                MaxHullReduction = maxHullReduction
            };
        }
    }

    /// <summary>
    /// Request for dockyard repair to restore permanent damage.
    /// </summary>
    public struct DockyardRepairRequest : IComponentData
    {
        /// <summary>
        /// Tick when request was made.
        /// </summary>
        public uint RequestTick;

        /// <summary>
        /// Priority (lower = higher priority).
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Target dockyard entity (if known).
        /// </summary>
        public Entity TargetDockyard;

        /// <summary>
        /// Whether currently in queue at a dockyard.
        /// </summary>
        public byte InQueue;

        /// <summary>
        /// Estimated ticks until repair complete.
        /// </summary>
        public uint EstimatedCompletionTicks;
    }

    /// <summary>
    /// Tag indicating entity is currently undergoing dockyard repair.
    /// </summary>
    public struct DockyardRepairInProgress : IComponentData
    {
        /// <summary>
        /// Dockyard performing the repair.
        /// </summary>
        public Entity Dockyard;

        /// <summary>
        /// Tick when repair started.
        /// </summary>
        public uint StartTick;

        /// <summary>
        /// Progress [0, 1].
        /// </summary>
        public half Progress;

        /// <summary>
        /// Estimated completion tick.
        /// </summary>
        public uint EstimatedCompletionTick;
    }

    /// <summary>
    /// Dockyard facility for full repairs.
    /// </summary>
    public struct DockyardFacility : IComponentData
    {
        /// <summary>
        /// Repair rate (hull points per second).
        /// </summary>
        public float RepairRate;

        /// <summary>
        /// Maximum vessels that can be repaired simultaneously.
        /// </summary>
        public byte MaxSimultaneous;

        /// <summary>
        /// Current vessels being repaired.
        /// </summary>
        public byte CurrentCount;

        /// <summary>
        /// Cost multiplier for repairs.
        /// </summary>
        public half CostMultiplier;

        /// <summary>
        /// Tech level of the dockyard (affects what can be repaired).
        /// </summary>
        public byte TechLevel;

        public static DockyardFacility Small => new DockyardFacility
        {
            RepairRate = 5f,
            MaxSimultaneous = 2,
            CurrentCount = 0,
            CostMultiplier = (half)1f,
            TechLevel = 1
        };

        public static DockyardFacility Medium => new DockyardFacility
        {
            RepairRate = 15f,
            MaxSimultaneous = 4,
            CurrentCount = 0,
            CostMultiplier = (half)0.9f,
            TechLevel = 2
        };

        public static DockyardFacility Large => new DockyardFacility
        {
            RepairRate = 40f,
            MaxSimultaneous = 8,
            CurrentCount = 0,
            CostMultiplier = (half)0.75f,
            TechLevel = 3
        };
    }

    /// <summary>
    /// Damage thresholds for hull system.
    /// </summary>
    public static class HullThresholds
    {
        /// <summary>
        /// Below this ratio, critical hits are more likely.
        /// </summary>
        public const float CriticalVulnerability = 0.3f;

        /// <summary>
        /// Below this ratio, vessel is combat ineffective.
        /// </summary>
        public const float CombatIneffective = 0.2f;

        /// <summary>
        /// Below this ratio, vessel is at risk of destruction.
        /// </summary>
        public const float DestructionRisk = 0.1f;

        /// <summary>
        /// Permanent damage above this requires dockyard repair.
        /// </summary>
        public const float PermanentDamageThreshold = 0.05f;

        /// <summary>
        /// Max hull reduction per critical hit.
        /// </summary>
        public const float MaxCriticalReduction = 0.1f;
    }

    /// <summary>
    /// Utility functions for hull damage calculations.
    /// </summary>
    public static class HullDamageUtility
    {
        /// <summary>
        /// Calculates actual damage after armor reduction.
        /// </summary>
        public static float CalculateDamage(float rawDamage, float armorRating)
        {
            float reduction = math.clamp(armorRating, 0f, 0.9f);
            return rawDamage * (1f - reduction);
        }

        /// <summary>
        /// Calculates critical hit chance based on current hull ratio.
        /// </summary>
        public static float GetCriticalChance(float hullRatio)
        {
            if (hullRatio >= HullThresholds.CriticalVulnerability)
            {
                return 0.05f; // Base 5% crit chance
            }

            // Increases as hull drops
            float vulnerability = (HullThresholds.CriticalVulnerability - hullRatio) / HullThresholds.CriticalVulnerability;
            return 0.05f + (vulnerability * 0.25f); // Up to 30% at very low hull
        }

        /// <summary>
        /// Calculates permanent hull reduction from a critical hit.
        /// </summary>
        public static float CalculatePermanentReduction(float severity, float baseMax)
        {
            float reductionRatio = severity * HullThresholds.MaxCriticalReduction;
            return baseMax * reductionRatio;
        }

        /// <summary>
        /// Gets repair time in seconds for permanent damage.
        /// </summary>
        public static float GetRepairTime(float permanentDamage, float repairRate)
        {
            if (repairRate <= 0f)
            {
                return float.MaxValue;
            }

            // Permanent damage takes longer to repair
            return (permanentDamage / repairRate) * 2f; // 2x time for permanent damage
        }

        /// <summary>
        /// Determines critical damage type based on random roll and hull state.
        /// </summary>
        public static CriticalDamageType RollCriticalType(uint seed, float hullRatio)
        {
            // Simple deterministic selection based on seed
            uint roll = seed % 8;
            return roll switch
            {
                0 => CriticalDamageType.HullBreach,
                1 => CriticalDamageType.ReactorDamage,
                2 => CriticalDamageType.EngineFailure,
                3 => CriticalDamageType.WeaponMalfunction,
                4 => CriticalDamageType.SensorBlindness,
                5 => CriticalDamageType.LifeSupportFailure,
                6 => CriticalDamageType.BridgeHit,
                7 => CriticalDamageType.AmmoCookoff,
                _ => CriticalDamageType.HullBreach
            };
        }
    }
}

