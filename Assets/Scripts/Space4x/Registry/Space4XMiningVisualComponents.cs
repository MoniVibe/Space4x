using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Simple primitive types used by the mining debug renderer.
    /// </summary>
    public enum Space4XMiningPrimitive : byte
    {
        Cube = 0,
        Sphere = 1,
        Capsule = 2,
        Cylinder = 3
    }

    /// <summary>
    /// Visual configuration for rendering Space4X mining entities.
    /// Baked once from authoring data and consumed by the debug render system.
    /// </summary>
    public struct Space4XMiningVisualConfig : IComponentData
    {
        public Space4XMiningPrimitive CarrierPrimitive;
        public Space4XMiningPrimitive MiningVesselPrimitive;
        public Space4XMiningPrimitive AsteroidPrimitive;

        public float CarrierScale;
        public float MiningVesselScale;
        public float AsteroidScale;

        public float4 CarrierColor;
        public float4 MiningVesselColor;
        public float4 AsteroidColor;
    }
}












