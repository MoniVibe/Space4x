using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Identity;
using UHash128 = UnityEngine.Hash128;

namespace PureDOTS.Authoring.Identity
{
    /// <summary>
    /// Authoring helper that assigns a stable id to an entity at bake time.
    /// Use an explicit stable string key (preferred) or let Unity provide a GUID-derived default.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityStableIdAuthoring : MonoBehaviour
    {
        [Tooltip("Optional explicit stable key. If empty, a GUID-derived key is used.")]
        [SerializeField] private string stableKey;

        private sealed class Baker : Baker<EntityStableIdAuthoring>
        {
            public override void Bake(EntityStableIdAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var key = authoring.stableKey;

                // Ensure determinism across imports when author didn't specify a key:
                // we still pin it to a scene + hierarchy path rather than relying on entity indices.
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = BuildFallbackKey(authoring);
                }

                var hashHex = UHash128.Compute(key.Trim()).ToString();
                var stableId = ParseHex128(hashHex);
                AddComponent(entity, new EntityStableId
                {
                    Hi = stableId.Hi,
                    Lo = stableId.Lo
                });
            }

            private static string BuildFallbackKey(EntityStableIdAuthoring authoring)
            {
                var scene = authoring.gameObject.scene;
                var sceneId = !string.IsNullOrEmpty(scene.path) ? scene.path : scene.name;
                var path = BuildHierarchyPath(authoring.transform);
                if (string.IsNullOrWhiteSpace(sceneId))
                {
                    return path;
                }

                return $"{sceneId}:{path}";
            }

            private static string BuildHierarchyPath(Transform transform)
            {
                if (transform == null)
                {
                    return string.Empty;
                }

                var path = transform.name;
                var current = transform.parent;
                while (current != null)
                {
                    path = $"{current.name}/{path}";
                    current = current.parent;
                }

                return path;
            }

            private static EntityStableId ParseHex128(string hex32)
            {
                // Unity's Hash128.ToString() is 32 hex chars.
                // We encode as Hi (first 16) + Lo (last 16).
                if (string.IsNullOrEmpty(hex32) || hex32.Length < 32)
                {
                    return default;
                }

                var hi = ulong.Parse(hex32.Substring(0, 16), System.Globalization.NumberStyles.HexNumber);
                var lo = ulong.Parse(hex32.Substring(16, 16), System.Globalization.NumberStyles.HexNumber);
                return new EntityStableId
                {
                    Hi = hi,
                    Lo = lo
                };
            }
        }
    }
}


