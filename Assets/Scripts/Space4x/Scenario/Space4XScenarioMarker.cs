using Unity.Entities;
using UnityEngine.Scripting.APIUpdating;

namespace Space4X.Scenario
{
    /// <summary>
    /// Tag component baked into legacy SubScenes to enable legacy validation systems.
    /// </summary>
    [MovedFrom(true, "Space4X.Demo", null, "Space4XDemoMarker")]
    public struct Space4XScenarioMarker : IComponentData
    {
    }
}
