using Unity.Entities;

namespace Space4X.Runtime
{
    public struct Space4XOrbitalBandConfig : IComponentData
    {
        public byte Enabled;
        public float InnerRadius;
        public float OuterRadius;
        public float DistanceScale;
        public float SpeedScale;
        public float RangeScale;
        public float EnterMultiplier;
        public float ExitMultiplier;

        public static Space4XOrbitalBandConfig Default => new Space4XOrbitalBandConfig
        {
            Enabled = 0,
            InnerRadius = 0f,
            OuterRadius = 0f,
            DistanceScale = 1.35f,
            SpeedScale = 1.15f,
            RangeScale = 1.25f,
            EnterMultiplier = 0.98f,
            ExitMultiplier = 1.02f
        };
    }

    public struct Space4XOrbitalBandRegion : IComponentData
    {
        public float InnerRadius;
        public float OuterRadius;
        public float DistanceScale;
        public float SpeedScale;
        public float RangeScale;
    }

    public struct Space4XOrbitalBandState : IComponentData
    {
        public Entity AnchorFrame;
        public float DistanceScale;
        public float SpeedScale;
        public float RangeScale;
        public byte InBand;
    }

    public struct Space4XOrbitalBandAnchorTag : IComponentData { }
}
