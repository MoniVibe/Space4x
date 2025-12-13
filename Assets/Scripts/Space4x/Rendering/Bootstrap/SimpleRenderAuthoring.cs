using Space4X.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Rendering.Bootstrap
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Minimal Entities Graphics bootstrap that spawns a grid of renderable entities using a single mesh/material.
    /// Drop this into a SubScene in Demo_Space4X_Render to validate URP + Entities Graphics wiring.
    /// </summary>
    public class SimpleRenderAuthoring : MonoBehaviour
    {
        public Mesh mesh;
        public Material material;
        public int countX = 5;
        public int countZ = 5;
        public float spacing = 2f;
        public ushort archetypeId = 1;
        public byte lod = 0;

        private void OnValidate()
        {
            countX = Mathf.Max(1, countX);
            countZ = Mathf.Max(1, countZ);
            spacing = Mathf.Max(0.1f, spacing);
        }

        #if SPACE4X_LEGACY_RENDER
        private class Baker : Baker<SimpleRenderAuthoring>
        {
            public override void Bake(SimpleRenderAuthoring authoring)
            {
                if (authoring.mesh == null || authoring.material == null)
                {
                    Debug.LogWarning("[SimpleRenderAuthoring] Mesh or Material is missing; nothing to bake.");
                    return;
                }

                var renderMeshArray = new RenderMeshArray(new[] { authoring.material }, new[] { authoring.mesh });
                var bounds = authoring.mesh.bounds.extents;
                var extents = new float3(bounds.x, bounds.y, bounds.z);

                // Shared entity carrying the RenderMeshArray
                var sharedEntity = GetEntity(TransformUsageFlags.None);
                AddSharedComponentManaged(sharedEntity, renderMeshArray);

                for (int x = 0; x < authoring.countX; x++)
                {
                    for (int z = 0; z < authoring.countZ; z++)
                    {
                        var position = new float3(x * authoring.spacing, 0f, z * authoring.spacing);
                        var entity = CreateAdditionalEntity(TransformUsageFlags.Renderable);
                        AddComponent(entity, LocalTransform.FromPosition(position));
                        AddComponent(entity, new RenderKey { ArchetypeId = authoring.archetypeId, LOD = authoring.lod });
                        AddComponent(entity, new RenderFlags { Visible = 1, ShadowCaster = 1, HighlightMask = 0 });
                        AddSharedComponentManaged(entity, renderMeshArray);
                        AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

                        var localBounds = new Unity.Mathematics.AABB { Center = float3.zero, Extents = extents };
                        var worldBounds = new Unity.Mathematics.AABB { Center = position, Extents = extents };
                        AddComponent(entity, new RenderBounds { Value = localBounds });
                        AddComponent(entity, new WorldRenderBounds { Value = worldBounds });
                        AddComponent(entity, new ChunkWorldRenderBounds { Value = worldBounds });
                    }
                }
            }
        }
        #endif
    }
}
