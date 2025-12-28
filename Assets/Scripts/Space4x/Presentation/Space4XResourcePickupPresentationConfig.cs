using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    public struct Space4XResourcePickupPresentationConfig : IComponentData
    {
        public float Lift;
        public float MinScale;
        public float MaxScale;
        public float AmountForMaxScale;
        public float BoundsExtents;
        public float Smoothing;
        public bool UseSourceEntityAlignment;
        public float3 LocalOffsetWhenAligned;

        public static Space4XResourcePickupPresentationConfig Default => new Space4XResourcePickupPresentationConfig
        {
            Lift = 0.35f,
            MinScale = 0.35f,
            MaxScale = 0.85f,
            AmountForMaxScale = 100f,
            BoundsExtents = 1.5f,
            Smoothing = 10f,
            UseSourceEntityAlignment = true,
            LocalOffsetWhenAligned = new float3(0f, 0.35f, 0f)
        };
    }

}
