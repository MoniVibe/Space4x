using Unity.Entities;

namespace Space4X.Presentation
{
    /// <summary>
    /// Presentation-only debug toggles for Space4X.
    /// </summary>
    public struct Space4XPresentationDebugConfig : IComponentData
    {
        public byte EnableAttackMoveDebugLines;
        public byte DisableDepthBobbing;
    }
}
