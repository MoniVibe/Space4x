using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Buffer element for tracer/beam visual requests.
    /// Used by presentation systems to render projectile trails and beam lines.
    /// </summary>
    public struct TracerVisualRequest : IBufferElementData
    {
        public float3 Start;
        public float3 End;
        public FixedString32Bytes StyleToken;
        public float Duration;
    }
}

