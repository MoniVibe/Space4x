using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    public struct Space4XCargoPresentationConfig : IComponentData
    {
        public float3 LocalOffset;
        public float BaseScale;
        public float MaxScale;
        public float Smoothing;
        public float BoundsExtents;

        public static Space4XCargoPresentationConfig Default => new Space4XCargoPresentationConfig
        {
            LocalOffset = new float3(0f, -0.5f, -0.9f),
            BaseScale = 0.6f,
            MaxScale = 1.0f,
            Smoothing = 8f,
            BoundsExtents = 0.75f
        };
    }

}
