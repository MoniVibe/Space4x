using Unity.Entities;

namespace Godgame.Rendering
{
    /// <summary>
    /// Tag added once a RenderKey entity has been processed by the Godgame render catalog system.
    /// Prevents redundant Entities Graphics uploads every frame.
    /// </summary>
    public struct RenderCatalogAppliedTag : IComponentData
    {
    }
}
