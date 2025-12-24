using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    /// <summary>
    /// Movement component for vessels (mining vessels, carriers, etc.)
    /// Similar to VillagerMovement but designed for ships
    /// </summary>
    public struct VesselMovement : IComponentData
    {
        public float3 Velocity;
        public float BaseSpeed;
        public float CurrentSpeed;
        public quaternion DesiredRotation;
        public byte IsMoving;
        public uint LastMoveTick;
    }

    /// <summary>
    /// AI state for vessels - tracks targets and goals
    /// </summary>
    public struct VesselAIState : IComponentData
    {
        public enum Goal : byte
        {
            None = 0,
            Mining = 1,
            Returning = 2,
            Idle = 3,
            Formation = 4,
            Patrol = 5,
            Escort = 6
        }

        public enum State : byte
        {
            Idle = 0,
            MovingToTarget = 1,
            Mining = 2,
            Returning = 3
        }

        public State CurrentState;
        public Goal CurrentGoal;
        public Entity TargetEntity; // Asteroid or carrier to target
        public float3 TargetPosition;
        public float StateTimer;
        public uint StateStartTick;
    }

    /// <summary>
    /// Binding that maps shared AI action indices to vessel goals.
    /// Similar to VillagerAIUtilityBinding but for vessels.
    /// </summary>
    public struct VesselAIUtilityBinding : IComponentData
    {
        public FixedList32Bytes<VesselAIState.Goal> Goals;
    }
}

