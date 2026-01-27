using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Presentation-only scale multiplier (registry-driven, not sim-owned).
    /// </summary>
    public struct PresentationScaleMultiplier : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Editor-only sentinel used to detect structural changes inside PresentationSystemGroup.
    /// </summary>
    public struct PresentationStructuralChangeSentinel : IComponentData
    {
        public int LastKnownOrderVersion;
    }

    /// <summary>
    /// Marks when presentation systems have completed their initial setup pass.
    /// </summary>
    public struct PresentationReady : IComponentData
    {
    }
}
