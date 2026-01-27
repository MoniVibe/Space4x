using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Visual placeholder type used to drive simple DOTS-only rendering rules.
    /// </summary>
    public enum PlaceholderVisualKind : byte
    {
        Crate = 0,
        Barrel = 1,
        Miracle = 2,
        Vegetation = 3
    }

    /// <summary>
    /// Basic placeholder metadata shared by all visual types.
    /// </summary>
    public struct PlaceholderVisual : IComponentData
    {
        public PlaceholderVisualKind Kind;
        public float BaseScale;
        public float3 LocalOffset;
    }

    /// <summary>
    /// Additional data for vegetation placeholders that should grow/shrink with lifecycle.
    /// </summary>
    public struct PlaceholderVegetationScale : IComponentData
    {
        public float SeedlingScale;
        public float GrowingScale;
        public float MatureScale;
        public float FruitingScale;
        public float DyingScale;
        public float DeadScale;
        public float LerpSeconds;
    }

    /// <summary>
    /// Tracks the current interpolated scale applied by <see cref="VegetationPlaceholderScaleSystem"/>.
    /// </summary>
    public struct PlaceholderVegetationScaleState : IComponentData
    {
        public float CurrentScale;
    }

    /// <summary>
    /// Lightweight pulse data for miracle placeholder glow.
    /// </summary>
    public struct MiraclePlaceholderPulse : IComponentData
    {
        public float4 BaseColor;
        public float BaseIntensity;
        public float PulseAmplitude;
        public float PulseSpeed;
        public float Phase;
    }
}
