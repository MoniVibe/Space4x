using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Swarms
{
    /// <summary>
    /// State for swarm-based thrust on ships or objects.
    /// Aggregates contributions from drones in Tug mode.
    /// </summary>
    public struct SwarmThrustState : IComponentData
    {
        /// <summary>Desired movement direction (normalized).</summary>
        public float3 DesiredDirection;
        
        /// <summary>Current aggregated thrust magnitude from drones.</summary>
        public float CurrentThrust;
        
        /// <summary>True if swarm thrust is active (engines offline, drones tugging).</summary>
        public bool Active;
    }
}

