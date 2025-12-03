using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime.Interaction
{
    /// <summary>
    /// Singleton component tracking the debug hand's grab state.
    /// Used for the god hand / grab & yeet debug tool.
    /// </summary>
    public struct HandState : IComponentData
    {
        /// <summary>
        /// Entity currently being grabbed (Entity.Null if none).
        /// </summary>
        public Entity Grabbed;

        /// <summary>
        /// Current grab distance from camera (adjusted via scroll wheel).
        /// </summary>
        public float GrabDistance;

        /// <summary>
        /// Local offset from entity position to grab point (world space).
        /// </summary>
        public float3 LocalOffset;

        /// <summary>
        /// Hand position from last frame (for computing throw velocity).
        /// </summary>
        public float3 LastFramePos;

        /// <summary>
        /// Whether the hand is currently grabbing an entity.
        /// </summary>
        public bool IsGrabbing;

        /// <summary>
        /// Current hand velocity (computed each frame during dragging).
        /// </summary>
        public float3 CurrentHandVel;

        /// <summary>
        /// Whether the hand is charging a slingshot throw (right mouse or modifier held).
        /// </summary>
        public bool IsCharging;

        /// <summary>
        /// Accumulated charge time for slingshot mechanics (in seconds).
        /// </summary>
        public float ChargeTime;

        /// <summary>
        /// Pull direction from grab point to current hand position (for slingshot visualization).
        /// </summary>
        public float3 PullDirection;
    }
}

