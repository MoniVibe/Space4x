using Unity.Entities;

namespace Space4X.Rendering
{
    /// <summary>
    /// Identifies which render archetype an entity should use and its current LOD bucket.
    /// This is game-local until the shared PureDOTS version lands.
    /// </summary>
    public struct RenderKey : IComponentData
    {
        public ushort ArchetypeId;
        public byte LOD;
    }

    /// <summary>
    /// Minimal render flags used by the Space4X rendering pipeline.
    /// </summary>
    public struct RenderFlags : IComponentData
    {
        public byte Visible;       // 0 = hidden
        public byte ShadowCaster;  // 0 = no shadows
        public byte HighlightMask; // bitmask for outlines/selection
    }

    /// <summary>
    /// Optional owner link for proxy/impostor entities.
    /// </summary>
    public struct RenderOwner : IComponentData
    {
        public Entity Owner;
    }

    /// <summary>
    /// Tag indicating the render catalog has already assigned Entities Graphics components.
    /// </summary>
    public struct RenderCatalogAppliedTag : IComponentData
    {
    }
}
