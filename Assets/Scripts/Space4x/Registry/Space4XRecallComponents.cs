using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Thresholds that trigger automatic vessel recall to carrier/base.
    /// When any threshold is breached, the vessel enters recall state.
    /// </summary>
    public struct RecallThresholds : IComponentData
    {
        /// <summary>
        /// Minimum ammo percentage before recall [0, 1].
        /// 0 = never recall for ammo, 1 = always recall.
        /// </summary>
        public half AmmoThreshold;

        /// <summary>
        /// Minimum fuel percentage before recall [0, 1].
        /// </summary>
        public half FuelThreshold;

        /// <summary>
        /// Minimum hull integrity percentage before recall [0, 1].
        /// </summary>
        public half HullThreshold;

        /// <summary>
        /// Whether recall thresholds are active.
        /// </summary>
        public byte Enabled;

        /// <summary>
        /// Parent carrier/base to return to.
        /// </summary>
        public Entity RecallTarget;

        public static RecallThresholds Default => new RecallThresholds
        {
            AmmoThreshold = (half)0.2f,  // Recall at 20% ammo
            FuelThreshold = (half)0.3f,  // Recall at 30% fuel
            HullThreshold = (half)0.4f,  // Recall at 40% hull
            Enabled = 1,
            RecallTarget = Entity.Null
        };

        public static RecallThresholds Aggressive => new RecallThresholds
        {
            AmmoThreshold = (half)0.1f,  // Push harder before recall
            FuelThreshold = (half)0.2f,
            HullThreshold = (half)0.25f,
            Enabled = 1,
            RecallTarget = Entity.Null
        };

        public static RecallThresholds Cautious => new RecallThresholds
        {
            AmmoThreshold = (half)0.4f,  // Return early
            FuelThreshold = (half)0.5f,
            HullThreshold = (half)0.6f,
            Enabled = 1,
            RecallTarget = Entity.Null
        };

        public static RecallThresholds Disabled => new RecallThresholds
        {
            AmmoThreshold = (half)0f,
            FuelThreshold = (half)0f,
            HullThreshold = (half)0f,
            Enabled = 0,
            RecallTarget = Entity.Null
        };
    }

    /// <summary>
    /// Current recall state for a vessel.
    /// </summary>
    public struct RecallState : IComponentData
    {
        /// <summary>
        /// Whether vessel is currently recalling.
        /// </summary>
        public byte IsRecalling;

        /// <summary>
        /// Reason for current recall.
        /// </summary>
        public RecallReason Reason;

        /// <summary>
        /// Tick when recall was triggered.
        /// </summary>
        public uint RecallStartTick;

        /// <summary>
        /// Current ammo level [0, 1].
        /// </summary>
        public half CurrentAmmo;

        /// <summary>
        /// Current fuel level [0, 1].
        /// </summary>
        public half CurrentFuel;

        /// <summary>
        /// Current hull integrity [0, 1].
        /// </summary>
        public half CurrentHull;

        public static RecallState Default => new RecallState
        {
            IsRecalling = 0,
            Reason = RecallReason.None,
            RecallStartTick = 0,
            CurrentAmmo = (half)1f,
            CurrentFuel = (half)1f,
            CurrentHull = (half)1f
        };

        public bool ShouldRecall(in RecallThresholds thresholds)
        {
            if (thresholds.Enabled == 0)
            {
                return false;
            }

            return (float)CurrentAmmo <= (float)thresholds.AmmoThreshold ||
                   (float)CurrentFuel <= (float)thresholds.FuelThreshold ||
                   (float)CurrentHull <= (float)thresholds.HullThreshold;
        }

        public RecallReason GetRecallReason(in RecallThresholds thresholds)
        {
            if ((float)CurrentHull <= (float)thresholds.HullThreshold)
            {
                return RecallReason.HullDamage;
            }
            if ((float)CurrentFuel <= (float)thresholds.FuelThreshold)
            {
                return RecallReason.LowFuel;
            }
            if ((float)CurrentAmmo <= (float)thresholds.AmmoThreshold)
            {
                return RecallReason.LowAmmo;
            }
            return RecallReason.None;
        }
    }

    /// <summary>
    /// Reason for vessel recall.
    /// </summary>
    public enum RecallReason : byte
    {
        None = 0,
        LowAmmo = 1,
        LowFuel = 2,
        HullDamage = 3,
        ManualOrder = 4,
        StanceChange = 5,
        JumpPrep = 6,
        CarrierRecall = 7
    }

    /// <summary>
    /// Vessel resource levels for recall evaluation.
    /// </summary>
    public struct VesselResourceLevels : IComponentData
    {
        /// <summary>
        /// Maximum ammo capacity.
        /// </summary>
        public float MaxAmmo;

        /// <summary>
        /// Current ammo count.
        /// </summary>
        public float CurrentAmmo;

        /// <summary>
        /// Maximum fuel capacity.
        /// </summary>
        public float MaxFuel;

        /// <summary>
        /// Current fuel amount.
        /// </summary>
        public float CurrentFuel;

        /// <summary>
        /// Maximum hull integrity.
        /// </summary>
        public float MaxHull;

        /// <summary>
        /// Current hull integrity.
        /// </summary>
        public float CurrentHull;

        public float AmmoRatio => MaxAmmo > 0 ? CurrentAmmo / MaxAmmo : 1f;
        public float FuelRatio => MaxFuel > 0 ? CurrentFuel / MaxFuel : 1f;
        public float HullRatio => MaxHull > 0 ? CurrentHull / MaxHull : 1f;

        public static VesselResourceLevels Default => new VesselResourceLevels
        {
            MaxAmmo = 100f,
            CurrentAmmo = 100f,
            MaxFuel = 100f,
            CurrentFuel = 100f,
            MaxHull = 100f,
            CurrentHull = 100f
        };

        public static VesselResourceLevels Create(float maxAmmo, float maxFuel, float maxHull)
        {
            return new VesselResourceLevels
            {
                MaxAmmo = maxAmmo,
                CurrentAmmo = maxAmmo,
                MaxFuel = maxFuel,
                CurrentFuel = maxFuel,
                MaxHull = maxHull,
                CurrentHull = maxHull
            };
        }
    }

    /// <summary>
    /// Utility helpers for recall logic.
    /// </summary>
    public static class RecallUtility
    {
        /// <summary>
        /// Checks if vessel should enter recall state.
        /// </summary>
        public static bool ShouldTriggerRecall(
            in RecallThresholds thresholds,
            in VesselResourceLevels levels,
            out RecallReason reason)
        {
            reason = RecallReason.None;

            if (thresholds.Enabled == 0)
            {
                return false;
            }

            // Check hull first (most critical)
            if (levels.HullRatio <= (float)thresholds.HullThreshold)
            {
                reason = RecallReason.HullDamage;
                return true;
            }

            // Check fuel second (stranded is bad)
            if (levels.FuelRatio <= (float)thresholds.FuelThreshold)
            {
                reason = RecallReason.LowFuel;
                return true;
            }

            // Check ammo last (least critical)
            if (levels.AmmoRatio <= (float)thresholds.AmmoThreshold)
            {
                reason = RecallReason.LowAmmo;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets urgency level for recall [0, 1]. Higher = more urgent.
        /// </summary>
        public static float GetRecallUrgency(RecallReason reason, in VesselResourceLevels levels)
        {
            return reason switch
            {
                RecallReason.HullDamage => 1f - levels.HullRatio,
                RecallReason.LowFuel => 1f - levels.FuelRatio,
                RecallReason.LowAmmo => 0.5f * (1f - levels.AmmoRatio),
                RecallReason.ManualOrder => 0.8f,
                RecallReason.CarrierRecall => 1f,
                RecallReason.JumpPrep => 1f,
                _ => 0f
            };
        }
    }
}

