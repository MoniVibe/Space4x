using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Hand
{
    /// <summary>
    /// Hand state type enum for state machine.
    /// </summary>
    public enum HandStateType : byte
    {
        Idle,
        Hovering,
        AttemptPick,  // Grab latch delay
        Holding,
        Aiming,  // Movement threshold exceeded
        Charging,  // Slingshot charge
        Releasing,  // Drop/throw/queue
        Siphoning,
        Dumping,
        CastingMiracle,
        Cooldown
    }

    /// <summary>
    /// Hand state component tracking current state machine state.
    /// </summary>
    public struct HandState : IComponentData
    {
        public HandStateType CurrentState;
        public HandStateType PreviousState;
        public Entity HeldEntity;
        public float3 HoldPoint;
        public float HoldDistance;  // Desired distance (from scroll)
        public float ChargeTimer;  // 0..1 normalized
        public float CooldownTimer;
        public ushort StateTimer;  // Ticks in current state
    }
}

