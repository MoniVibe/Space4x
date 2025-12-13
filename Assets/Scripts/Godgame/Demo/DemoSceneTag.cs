using Unity.Entities;

namespace Godgame.Demo
{
    /// <summary>
    /// Tag component baked into demo scenes so editor-only systems can gate themselves.
    /// </summary>
    public struct DemoSceneTag : IComponentData
    {
    }
}
