using Unity.Entities;

namespace PureDOTS.Runtime.Swarms
{
    /// <summary>
    /// Swarm behavior mode for drones.
    /// </summary>
    public enum SwarmMode : byte
    {
        Screen,  // Orbit in defensive pattern
        Attack,  // Spiral / dive onto target
        Return,  // Go back to orbit
        Tug      // Push/pull mother ship or object
    }

    /// <summary>
    /// Behavior state for swarm drones.
    /// Controls how drones move relative to their orbit.
    /// </summary>
    public struct SwarmBehavior : IComponentData
    {
        /// <summary>Current behavior mode.</summary>
        public SwarmMode Mode;
        
        /// <summary>Target entity (for Attack or Tug modes).</summary>
        public Entity Target;
    }
}

