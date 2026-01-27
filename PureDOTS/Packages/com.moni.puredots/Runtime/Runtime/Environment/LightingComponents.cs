using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Environment
{
    /// <summary>
    /// Lighting state computed from sun angle and time of day.
    /// </summary>
    public struct LightingState : IComponentData
    {
        /// <summary>Sun angle in radians (0 = sunrise, π = noon, 2π = sunset).</summary>
        public float SunAngle;
        /// <summary>Sun intensity (0-1).</summary>
        public float SunIntensity;
        /// <summary>Sun color (RGB).</summary>
        public float3 SunColor;
        /// <summary>Ambient intensity (0-1).</summary>
        public float AmbientIntensity;
    }
}

