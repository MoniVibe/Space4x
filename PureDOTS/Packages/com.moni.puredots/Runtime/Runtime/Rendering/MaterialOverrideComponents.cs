using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Rendering
{
    /// <summary>
    /// Desired per-instance base color for URP materials. Applied by <see cref="PureDOTS.Systems.Rendering.MaterialOverrideSystem"/>.
    /// </summary>
    public struct MaterialColorOverride : IComponentData
    {
        public float4 Value;
    }

    /// <summary>
    /// Desired per-instance emission color for URP materials.
    /// </summary>
    public struct MaterialEmissionOverride : IComponentData
    {
        public float4 Value;
    }
}

