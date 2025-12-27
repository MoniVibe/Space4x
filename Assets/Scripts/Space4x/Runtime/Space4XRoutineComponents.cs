using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public enum Space4XRoutineGoal : byte
    {
        None = 0,
        Mining = 1,
        MiningSupport = 2,
        Patrol = 3,
        Escort = 4,
        Standby = 5
    }

    public enum Space4XRoutinePhase : byte
    {
        Idle = 0,
        Depart = 1,
        Transit = 2,
        Approach = 3,
        Work = 4,
        Return = 5,
        Dock = 6,
        Hold = 7
    }

    public enum Space4XRoutineDirectiveKind : byte
    {
        None = 0,
        HoldPosition = 1,
        ApproachTarget = 2,
        Mine = 3,
        ReturnToCarrier = 4,
        Dock = 5,
        Undock = 6,
        Patrol = 7,
        Escort = 8
    }

    /// <summary>
    /// Lightweight routine state for pilots/captains to expose phased behavior in headless runs.
    /// </summary>
    public struct Space4XRoutineState : IComponentData
    {
        public Space4XRoutineGoal Goal;
        public Space4XRoutinePhase Phase;
        public Space4XRoutineDirectiveKind Directive;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public uint PhaseStartTick;
        public uint LastDirectiveTick;
    }
}
