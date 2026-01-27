using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Combat position in 3D space.
    /// </summary>
    public struct CombatPosition3D : IComponentData
    {
        public float3 Position;
        public float VerticalOffset;
    }

    /// <summary>
    /// Vertical engagement range - range for vertical combat.
    /// </summary>
    public struct VerticalEngagementRange : IComponentData
    {
        public float VerticalRange;
        public float HorizontalRange;
    }

    /// <summary>
    /// 3D advantage - positioning bonuses.
    /// </summary>
    public struct Advantage3D : IComponentData
    {
        public float HighGroundBonus; // 0-1 multiplier for being above target
        public float FlankingBonus; // 0-1 multiplier for flanking from below/above
        public float VerticalAdvantage; // Combined advantage value
    }

    /// <summary>
    /// Vertical movement state for 3D combat positioning.
    /// </summary>
    public struct VerticalMovementState : IComponentData
    {
        public VerticalMovementMode Mode;
        public float TargetAltitude;
        public float CurrentAltitude;
        public float VerticalSpeed;
    }

    /// <summary>
    /// Vertical movement modes.
    /// </summary>
    public enum VerticalMovementMode : byte
    {
        None = 0,
        Ascend = 1,
        Descend = 2,
        Dive = 3, // Fast descend
        Climb = 4 // Fast ascend
    }
}

