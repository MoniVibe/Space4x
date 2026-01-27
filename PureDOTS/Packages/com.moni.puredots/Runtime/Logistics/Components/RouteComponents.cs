using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics.Components
{
    /// <summary>
    /// Waypoint kind for route planning.
    /// </summary>
    public enum WaypointKind : byte
    {
        StaticPoint = 0,
        Depot = 1,
        TargetEntity = 2, // moving entity (army/fleet)
        RendezvousPoint = 3 // precomputed rendezvous
    }

    /// <summary>
    /// Waypoint element buffer.
    /// Represents a single waypoint in a route.
    /// </summary>
    public struct WaypointElement : IBufferElementData
    {
        public WaypointKind Kind;
        public float3 Position; // for Static/Rendezvous
        public Entity Target; // for Depot/TargetEntity
    }

    /// <summary>
    /// Route plan component.
    /// Contains route metadata and waypoints.
    /// </summary>
    public struct RoutePlan : IComponentData
    {
        public int JobId;
        public float EstimatedDistance;
        public uint EstimatedArrivalTick;
    }

    /// <summary>
    /// Haul assignment state.
    /// </summary>
    public enum HaulAssignmentState : byte
    {
        Idle = 0,
        EnRoute = 1,
        Loading = 2,
        Unloading = 3,
        Waiting = 4
    }

    /// <summary>
    /// Haul assignment component.
    /// Tracks a hauler's current job assignment and progress.
    /// </summary>
    public struct HaulAssignment : IComponentData
    {
        public int JobId;
        public int CurrentWaypointIndex;
        public HaulAssignmentState State;
    }
}

