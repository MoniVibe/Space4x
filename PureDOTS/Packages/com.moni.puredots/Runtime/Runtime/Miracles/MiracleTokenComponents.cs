using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Miracles
{
    /// <summary>
    /// Component on throw token entities containing miracle-specific data.
    /// </summary>
    public struct MiracleToken : IComponentData
    {
        /// <summary>Miracle ID that created this token.</summary>
        public MiracleId Id;
        
        /// <summary>Entity that cast this miracle (owner/caster).</summary>
        public Entity Owner;
        
        /// <summary>Intensity multiplier (from charge).</summary>
        public float Intensity;
        
        /// <summary>Effect radius (from charge).</summary>
        public float Radius;
        
        /// <summary>Initial launch velocity (for reference/debug).</summary>
        public float3 LaunchVelocity;
    }

    /// <summary>
    /// Component on throw token entities for impact detection.
    /// </summary>
    public struct MiracleOnImpact : IComponentData
    {
        /// <summary>Explosion/effect radius on impact.</summary>
        public float ExplosionRadius;
        
        /// <summary>Whether impact has occurred (0/1).</summary>
        public byte HasImpacted;
        
        /// <summary>Maximum flight time before timeout (seconds).</summary>
        public float MaxFlightTime;
        
        /// <summary>Time since launch (seconds).</summary>
        public float FlightTime;
    }
}

