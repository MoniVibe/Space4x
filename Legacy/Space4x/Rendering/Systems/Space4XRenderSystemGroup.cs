using Unity.Entities;
using Unity.Rendering;

namespace Space4X.Rendering.Systems
{
    /// <summary>
    /// Space4X-specific render system group that runs before Entities Graphics.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateBefore(typeof(Unity.Rendering.EntitiesGraphicsSystem))]
    public partial class Space4XRenderSystemGroup : ComponentSystemGroup
    {
    }
}
