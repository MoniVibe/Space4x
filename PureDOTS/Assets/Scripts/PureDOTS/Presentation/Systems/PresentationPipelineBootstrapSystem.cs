using PureDOTS.Presentation.Runtime;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace PureDOTS.Presentation.Systems
{
    /// <summary>
    /// Creates the shared render resources and catalog required for placeholder presentation.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PresentationPipelineBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = true;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<PresentationRenderCatalog>())
            {
                state.Enabled = false;
                return;
            }

            var assets = Runtime.PresentationRenderFactory.CreateFallbackAssets();
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<PresentationConfigTag>(entity);
            state.EntityManager.AddComponentData(entity, new PresentationRenderCatalog { Blob = assets.Catalog });
            state.EntityManager.AddSharedComponentManaged(entity, assets.RenderArray);

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<PresentationRenderCatalog>(out var catalogEntity))
            {
                var catalog = SystemAPI.GetComponent<PresentationRenderCatalog>(catalogEntity);
                if (catalog.Blob.IsCreated)
                {
                    catalog.Blob.Dispose();
                }

                var renderArray = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(catalogEntity);
                var meshes = renderArray.MeshReferences;
                for (int i = 0; i < meshes?.Length; i++)
                {
                    var mesh = meshes[i].Value;
                    if (mesh != null)
                    {
#if UNITY_EDITOR
                        Object.DestroyImmediate(mesh);
#else
                        Object.Destroy(mesh);
#endif
                    }
                }

                var materials = renderArray.MaterialReferences;
                for (int i = 0; i < materials?.Length; i++)
                {
                    var mat = materials[i].Value;
                    if (mat != null)
                    {
#if UNITY_EDITOR
                        Object.DestroyImmediate(mat);
#else
                        Object.Destroy(mat);
#endif
                    }
                }
            }
        }
    }
}
