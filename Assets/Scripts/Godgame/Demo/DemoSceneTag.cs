using Unity.Entities;

namespace Godgame.Scenario
{
    /// <summary>
    /// Tag component baked into demo scenes so editor-only systems can gate themselves.
    /// </summary>
    public struct DemoSceneTag : IComponentData
    {
    }
}
