using Unity.Entities;

namespace PureDOTS.Runtime.Vehicles
{
    /// <summary>
    /// Role classification for craft/vessels.
    /// </summary>
    public enum CraftRole : byte
    {
        Interceptor,
        Bomber,
        StrikeFighter,
        Recon,
        MiningVessel,
        UtilityShuttle
    }

    /// <summary>
    /// Craft frame definition - data-driven template for strike craft, mining vessels, etc.
    /// Designers define these in ScriptableObjects/JSON; loaded into blob assets.
    /// </summary>
    public struct CraftFrameDef : IComponentData
    {
        public int FrameId;
        public CraftRole Role;
        public float BaseMass;
        public float BaseHP;
        public float MaxCrew;
        public float AgilityRating;      // Maneuverability
        public float EnduranceRating;   // Fuel/operational duration
        public float FocusAssist;       // How well frame supports multitasking (HUD, AI)
        public int DefaultLoadoutId;    // References a module preset
    }

    /// <summary>
    /// Attack run behavior profile for strike craft.
    /// Defines approach, commit, and break-off parameters.
    /// </summary>
    public struct AttackRunProfile : IComponentData
    {
        public int FrameId;
        public float ApproachDistance;      // How far to start run
        public float BreakDistance;          // How close before break-off
        public float MinHullPercentToCommit; // Minimum hull % to commit to run
        public float MinAmmoPercentToCommit; // Minimum ammo % to commit to run
        public float FlakRiskTolerance;      // How much PD fire before abort
    }

    /// <summary>
    /// Mining pattern behavior profile for mining vessels.
    /// Defines optimal range, drill angles, cycle duration, retreat thresholds.
    /// </summary>
    public struct MiningPatternProfile : IComponentData
    {
        public int FrameId;
        public float OptimalRange;          // Optimal mining distance
        public float MaxDrillAngle;          // Maximum drill angle
        public float CycleDurationTicks;     // Mining cycle duration
        public float RetreatHullThreshold;   // Hull % threshold for retreat
    }

    /// <summary>
    /// Reference to a craft frame definition.
    /// Attached to craft entities to identify their frame type.
    /// </summary>
    public struct CraftFrameRef : IComponentData
    {
        public int FrameId;
    }
}

