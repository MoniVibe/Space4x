using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    public struct Space4XCarrierStorageMarkerPresentationConfig : IComponentData
    {
        public float3 LocalOffset;
        public float BaseScale;
        public float MaxScale;
        public float Smoothing;
        public float BoundsExtents;
        public float DepletedThreshold;
        public bool UseHysteresis;
        public float ShowFillThreshold;
        public float HideFillThreshold;
        public float PulseDuration;
        public float PulseIntensity;

        public static Space4XCarrierStorageMarkerPresentationConfig Default => new Space4XCarrierStorageMarkerPresentationConfig
        {
            LocalOffset = new float3(0f, 1.35f, 0f),
            BaseScale = 0.55f,
            MaxScale = 1.10f,
            Smoothing = 8f,
            BoundsExtents = 3f,
            DepletedThreshold = 0.02f,
            UseHysteresis = true,
            ShowFillThreshold = 0.03f,
            HideFillThreshold = 0.01f,
            PulseDuration = 0.35f,
            PulseIntensity = 0.5f
        };
    }

}
