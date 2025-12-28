using Unity.Entities;

namespace Space4X.Presentation
{
    public struct Space4XResourceMarkerPresentationConfig : IComponentData
    {
        public float BaseScale;
        public float MaxScale;
        public float OffsetMultiplier;
        public float Smoothing;
        public float BoundsExtents;
        public float DepletedThreshold;
        public byte UseCameraHemisphere;

        public static Space4XResourceMarkerPresentationConfig Default => new Space4XResourceMarkerPresentationConfig
        {
            BaseScale = 0.6f,
            MaxScale = 1.2f,
            OffsetMultiplier = 1.05f,
            Smoothing = 8f,
            BoundsExtents = 2f,
            DepletedThreshold = 0.01f,
            UseCameraHemisphere = 1
        };
    }

}
