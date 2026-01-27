using Unity.Entities;

namespace PureDOTS.Runtime.Core
{
    /// <summary>
    /// Tag component added to the canonical Game World so gameplay systems can gate themselves.
    /// </summary>
    public struct GameWorldTag : IComponentData
    {
    }
}
