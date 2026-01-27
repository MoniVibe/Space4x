using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Swarms
{
    /// <summary>
    /// Tag component marking an entity as a drone.
    /// </summary>
    public struct DroneTag : IComponentData { }

    /// <summary>
    /// Orbital parameters for drones orbiting around a ship or anchor.
    /// Similar to OrbitParams but optimized for small-scale swarm behavior.
    /// </summary>
    public struct DroneOrbit : IComponentData
    {
        /// <summary>Entity to orbit around (ship, carrier, etc.).</summary>
        public Entity AnchorShip;
        
        /// <summary>Orbital radius (typically small, e.g., 5-20 units).</summary>
        public float Radius;
        
        /// <summary>Angular speed in radians per second (typically higher than large orbits).</summary>
        public float AngularSpeed;
        
        /// <summary>Phase offset to vary drone positions in the swarm.</summary>
        public float PhaseOffset;
        
        /// <summary>Elevation offset for volumetric cloud effect (small +/- variation).</summary>
        public float Elevation;
    }
}

