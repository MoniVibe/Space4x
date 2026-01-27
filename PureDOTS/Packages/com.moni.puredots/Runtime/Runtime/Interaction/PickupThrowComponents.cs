using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Interaction
{
    /// <summary>
    /// Marks an entity as allowed to be grabbed by the player/god.
    /// </summary>
    public struct Pickable : IComponentData { }

    /// <summary>
    /// Marks entity currently being held by a "hand" / player entity.
    /// </summary>
    public struct HeldByPlayer : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// Player/camera/hand entity holding this entity.
        /// </summary>
        public Entity Holder;

        /// <summary>
        /// Local offset from entity origin to grab point (entity local space).
        /// </summary>
        public float3 LocalOffset;

        /// <summary>
        /// World position when pickup started.
        /// </summary>
        public float3 HoldStartPosition;

        /// <summary>
        /// Time when pickup started (in seconds).
        /// </summary>
        public float HoldStartTime;
    }

    /// <summary>
    /// Indicates entity movement should be skipped by movement systems.
    /// </summary>
    public struct MovementSuppressed : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Marks entity in flight due to a throw.
    /// </summary>
    public struct BeingThrown : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// Initial velocity when thrown.
        /// </summary>
        public float3 InitialVelocity;

        /// <summary>
        /// Time since throw started (in seconds).
        /// </summary>
        public float TimeSinceThrow;

        /// <summary>
        /// Previous position before integration (for tunneling prevention sweep tests).
        /// Updated by ThrownObjectTransformIntegratorSystem before moving.
        /// </summary>
        public float3 PrevPosition;

        /// <summary>
        /// Previous rotation before integration (used for swept-orientation casts).
        /// </summary>
        public quaternion PrevRotation;
    }

    /// <summary>
    /// State machine for pickup/throw interaction.
    /// </summary>
    public struct PickupState : IComponentData
    {
        /// <summary>
        /// Current state of the pickup interaction.
        /// </summary>
        public PickupStateType State;

        /// <summary>
        /// Last raycast position for cursor movement tracking.
        /// </summary>
        public float3 LastRaycastPosition;

        /// <summary>
        /// Distance from the cursor ray origin to the grab point.
        /// </summary>
        public float HoldDistance;

        /// <summary>
        /// Accumulated cursor movement (in world space units).
        /// </summary>
        public float CursorMovementAccumulator;

        /// <summary>
        /// Time holding RMB (in seconds).
        /// </summary>
        public float HoldTime;

        /// <summary>
        /// Velocity accumulated during throw priming.
        /// </summary>
        public float3 AccumulatedVelocity;

        /// <summary>
        /// Whether player is moving while holding.
        /// </summary>
        public bool IsMoving;

        /// <summary>
        /// Entity currently being targeted/held.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Last holder position for movement detection.
        /// </summary>
        public float3 LastHolderPosition;
    }

    /// <summary>
    /// State types for pickup/throw interaction.
    /// </summary>
    public enum PickupStateType : byte
    {
        /// <summary>
        /// No interaction active.
        /// </summary>
        Empty = 0,

        /// <summary>
        /// About to pick up (RMB down, waiting for cursor movement >3px).
        /// </summary>
        AboutToPick = 1,

        /// <summary>
        /// Currently holding an entity.
        /// </summary>
        Holding = 2,

        /// <summary>
        /// Primed to throw (moving while holding).
        /// </summary>
        PrimedToThrow = 3,

        /// <summary>
        /// Queued for throw (Shift+RMB release).
        /// </summary>
        Queued = 4
    }

    /// <summary>
    /// Optional: slingshot charge data on the player/god entity.
    /// </summary>
    public struct ThrowCharge : IComponentData
    {
        /// <summary>
        /// Accumulated charge time/energy.
        /// </summary>
        public float Charge;

        /// <summary>
        /// Maximum charge value.
        /// </summary>
        public float MaxCharge;

        /// <summary>
        /// Charge rate per second.
        /// </summary>
        public float ChargeRate;

        /// <summary>
        /// Whether currently charging.
        /// </summary>
        public bool IsCharging;
    }

    /// <summary>
    /// Entry in the throw queue.
    /// </summary>
    public struct ThrowQueueEntry
    {
        /// <summary>
        /// Target entity to throw.
        /// </summary>
        public Entity Target;

        /// <summary>
        /// Throw direction.
        /// </summary>
        public float3 Direction;

        /// <summary>
        /// Throw force/speed.
        /// </summary>
        public float Force;
    }

    /// <summary>
    /// Buffer element for throw queue.
    /// </summary>
    public struct ThrowQueue : IBufferElementData
    {
        /// <summary>
        /// Queue entry value.
        /// </summary>
        public ThrowQueueEntry Value;
    }
}






















