using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Space4X.Scenario
{
    /// <summary>
    /// Authoring component to mark legacy SubScenes so validation systems can run conditionally.
    /// </summary>
    [MovedFrom(true, "Space4X.Demo", null, "Space4XDemoMarkerAuthoring")]
    public class Space4XScenarioMarkerAuthoring : MonoBehaviour
    {
        private class Baker : Baker<Space4XScenarioMarkerAuthoring>
        {
            public override void Bake(Space4XScenarioMarkerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<Space4XScenarioMarker>(entity);
            }
        }
    }
}
