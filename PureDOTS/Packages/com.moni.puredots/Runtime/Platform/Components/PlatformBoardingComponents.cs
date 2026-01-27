using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Platform
{
    /// <summary>
    /// Docking method for boarding.
    /// </summary>
    public enum DockingMethod : byte
    {
        SoftDock = 0,
        HardDock = 1,
        Penetration = 2,
        Teleport = 3
    }

    /// <summary>
    /// Boarding phase.
    /// </summary>
    public enum BoardingPhase : byte
    {
        Approaching = 0,
        Breaching = 1,
        Fighting = 2,
        Resolution = 3
    }

    /// <summary>
    /// Docking link between attacker and defender platforms.
    /// </summary>
    public struct DockingLink : IComponentData
    {
        public Entity AttackerPlatform;
        public Entity DefenderPlatform;
        public float3 DockingPointWorld;
        public DockingMethod Method;
    }

    /// <summary>
    /// Boarding state on a platform under attack.
    /// </summary>
    public struct BoardingState : IComponentData
    {
        public int AttackerFactionId;
        public int DefenderFactionId;
        public BoardingPhase Phase;
    }

    /// <summary>
    /// Per-segment control tracking.
    /// </summary>
    public struct SegmentControl : IBufferElementData
    {
        public int SegmentIndex;
        public int FactionId;
        public float ControlLevel;
    }

    /// <summary>
    /// Boarding team in a segment.
    /// </summary>
    public struct BoardingTeam : IBufferElementData
    {
        public int FactionId;
        public int SegmentIndex;
        public int Count;
        public float Morale;
    }
}

