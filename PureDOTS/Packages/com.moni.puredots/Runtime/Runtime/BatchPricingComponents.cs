using Unity.Entities;

namespace PureDOTS.Runtime.Economy
{
    public struct BatchPricingState : IComponentData
    {
        public float LastPriceMultiplier;
        public uint LastUpdateTick;
        public float SmoothedDelta;
        public float LastUnits;
        public float SmoothedFill;
    }

    public struct BatchPricingConfig : IComponentData
    {
        public float MinMultiplier;
        public float MaxMultiplier;
        public float LowFillThreshold;
        public float HighFillThreshold;
        public float Elasticity;
        public float TrendSmoothing;
        public float MaxDeltaFraction;

        public static BatchPricingConfig CreateDefault()
        {
            return new BatchPricingConfig
            {
                MinMultiplier = 0.8f,
                MaxMultiplier = 1.4f,
                LowFillThreshold = 0.25f,
                HighFillThreshold = 0.9f,
                Elasticity = 1.0f,
                TrendSmoothing = 0.25f,
                MaxDeltaFraction = 0.35f
            };
        }
    }
}
