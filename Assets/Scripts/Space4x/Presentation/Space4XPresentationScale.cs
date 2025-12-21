using Unity.Entities;

namespace Space4X.Presentation
{
    /// <summary>
    /// Optional per-entity presentation scale override (world units multiplier).
    /// </summary>
    public struct PresentationScale : IComponentData
    {
        public float Value;
    }
}
