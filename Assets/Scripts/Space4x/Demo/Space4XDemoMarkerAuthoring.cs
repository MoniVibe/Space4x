using Unity.Entities;
using UnityEngine;

namespace Space4X.Demo
{
    /// <summary>
    /// Authoring component to mark demo SubScenes so validation systems can run conditionally.
    /// </summary>
    public class Space4XDemoMarkerAuthoring : MonoBehaviour
    {
        private class Baker : Baker<Space4XDemoMarkerAuthoring>
        {
            public override void Bake(Space4XDemoMarkerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<Space4XDemoMarker>(entity);
            }
        }
    }
}
