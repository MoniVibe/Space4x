using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Orbits
{
    /// <summary>
    /// Orbital parameters for analytic circular orbits.
    /// Position is computed deterministically as f(time, params) - no numerical integration.
    /// </summary>
    public struct OrbitParams : IComponentData
    {
        /// <summary>Entity to orbit around (planet, star, mothership, etc.).</summary>
        public Entity Anchor;
        
        /// <summary>Offset from anchor's origin, usually (0,0,0).</summary>
        public float3 LocalCenter;
        
        /// <summary>Orbital radius for circular orbit.</summary>
        public float Radius;
        
        /// <summary>Angular speed in radians per second.</summary>
        public float AngularSpeed;
        
        /// <summary>Starting angle at t=0 in radians.</summary>
        public float InitialPhase;
        
        /// <summary>Tilt angle (inclination) in radians.</summary>
        public float Inclination;
    }
}

