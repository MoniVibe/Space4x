using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Villager
{
    /// <summary>
    /// Pre-computed job state snapshot for hot path execution.
    /// Hot path: Follow current job step, perform job anim/loop with simple timers.
    /// No job re-selection every tick.
    /// </summary>
    public struct JobStateSnapshot : IComponentData
    {
        /// <summary>
        /// Current job step/phase.
        /// </summary>
        public JobStep CurrentStep;

        /// <summary>
        /// Work location (where to go/work).
        /// </summary>
        public float3 WorkLocation;

        /// <summary>
        /// Target entity for work (resource, storehouse, etc.).
        /// </summary>
        public Entity WorkTargetEntity;

        /// <summary>
        /// Simple timer for job animations/loops.
        /// </summary>
        public float JobTimer;

        /// <summary>
        /// Whether job is active (1) or idle (0).
        /// </summary>
        public byte IsActive;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Job step/phase enumeration.
    /// </summary>
    public enum JobStep : byte
    {
        Idle = 0,
        MovingToWork = 1,
        Working = 2,
        MovingToDelivery = 3,
        Delivering = 4,
        Returning = 5,
        Completed = 6
    }
}

