using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Navigation
{
    /// <summary>
    /// Group navigation component - tracks navigation path for a group (band, army, fleet).
    /// Groups own the NavPath; individuals follow with steering logic.
    /// </summary>
    public struct GroupNavComponent : IComponentData
    {
        /// <summary>
        /// Current navigation path for the group.
        /// </summary>
        public Entity NavPathEntity;

        /// <summary>
        /// Next waypoint/segment index the group is moving toward.
        /// </summary>
        public int NextWaypointIndex;

        /// <summary>
        /// Current target position for the group (from current segment).
        /// </summary>
        public float3 CurrentTargetPosition;

        /// <summary>
        /// Whether group navigation is active.
        /// </summary>
        public byte IsActive;
    }
}






















