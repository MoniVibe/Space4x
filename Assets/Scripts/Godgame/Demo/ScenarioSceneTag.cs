using Unity.Entities;
using UnityEngine.Scripting.APIUpdating;

namespace Godgame.Scenario
{
    /// <summary>
    /// Tag component baked into scenario scenes so editor-only systems can gate themselves.
    /// </summary>
    [MovedFrom(true, "Godgame.Scenario", null, "DemoSceneTag")]
    public struct ScenarioSceneTag : IComponentData
    {
    }
}
