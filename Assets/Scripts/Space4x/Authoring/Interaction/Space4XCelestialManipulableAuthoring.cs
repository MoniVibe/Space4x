using Space4X.Runtime.Interaction;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring.Interaction
{
    public sealed class Space4XCelestialManipulableAuthoring : MonoBehaviour
    {
        private sealed class Baker : Baker<Space4XCelestialManipulableAuthoring>
        {
            public override void Bake(Space4XCelestialManipulableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<Space4XCelestialManipulable>(entity);
            }
        }
    }
}
