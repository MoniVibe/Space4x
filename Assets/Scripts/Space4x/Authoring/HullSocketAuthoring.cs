using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that creates socket child transforms for hull attachment points.
    /// Sockets are created as child GameObjects with naming pattern: Socket_<MountType>_<Size>_<Index>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Hull Socket Authoring")]
    public sealed class HullSocketAuthoring : MonoBehaviour
    {
        [Tooltip("If true, automatically create sockets from catalog data (requires HullIdAuthoring)")]
        public bool autoCreateFromCatalog = true;

        [Tooltip("Manual socket definitions (used if autoCreateFromCatalog is false)")]
        public SocketDefinition[] manualSockets = new SocketDefinition[0];

        [System.Serializable]
        public class SocketDefinition
        {
            public MountType type;
            public MountSize size;
            public Vector3 localPosition;
            public Vector3 localRotationEuler;
        }

        public sealed class Baker : Unity.Entities.Baker<HullSocketAuthoring>
        {
            public override void Bake(HullSocketAuthoring authoring)
            {
                // Sockets are handled at authoring time, not at bake time
                // The Prefab Maker will create the socket child transforms
                // This component just marks the hull as having socket support
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<Registry.HullSocketTag>(entity);
            }
        }
    }
}

