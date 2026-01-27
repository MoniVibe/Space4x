using PureDOTS.Environment;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring for a flat terrain surface provider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainFlatSurfaceAuthoring : MonoBehaviour
    {
        public bool enabledProvider = true;
        public float height = 0f;
    }

    public sealed class TerrainFlatSurfaceBaker : Baker<TerrainFlatSurfaceAuthoring>
    {
        public override void Bake(TerrainFlatSurfaceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var worldHeight = authoring.transform.position.y + authoring.height;

            AddComponent(entity, new TerrainFlatSurface
            {
                Height = worldHeight,
                Enabled = (byte)(authoring.enabledProvider ? 1 : 0)
            });
        }
    }

    /// <summary>
    /// Authoring for a solid spherical volume provider (validation-only).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainSolidSphereAuthoring : MonoBehaviour
    {
        public bool enabledProvider = true;
        [Min(0.1f)] public float radius = 10f;
    }

    public sealed class TerrainSolidSphereBaker : Baker<TerrainSolidSphereAuthoring>
    {
        public override void Bake(TerrainSolidSphereAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var scale = authoring.transform.lossyScale;
            var scaleMax = math.cmax(scale);

            AddComponent(entity, new TerrainSolidSphere
            {
                Center = authoring.transform.position,
                Radius = math.max(0.01f, authoring.radius * scaleMax),
                Enabled = (byte)(authoring.enabledProvider ? 1 : 0)
            });
        }
    }
}
