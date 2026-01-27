using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Ensures a render catalog exists at runtime even if no SubScene baked it.
    /// Primarily used for headless simulations or runtime scene swaps.
    /// </summary>
    [DisallowMultipleComponent]
    public class RenderPresentationCatalogRuntimeBootstrap : MonoBehaviour
    {
        public RenderPresentationCatalogDefinition CatalogDefinition;

        private Entity _catalogEntity = Entity.Null;
        private Entity _renderMeshEntity = Entity.Null;
        private BlobAssetReference<RenderPresentationCatalogBlob> _catalogBlob;

        private void Awake()
        {
            if (CatalogDefinition == null)
            {
                Debug.LogError("[RenderPresentationCatalogRuntimeBootstrap] CatalogDefinition is missing.");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[RenderPresentationCatalogRuntimeBootstrap] Default world is not ready.");
                return;
            }

            var entityManager = world.EntityManager;
            var catalogQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RenderPresentationCatalog>());
            if (catalogQuery.CalculateEntityCount() > 0)
            {
                catalogQuery.Dispose();
                return;
            }
            catalogQuery.Dispose();

            var input = CatalogDefinition.ToBuildInput();
            if (!RenderPresentationCatalogBuilder.TryBuild(input, Allocator.Persistent, out var blobRef, out var renderMeshArray))
            {
                return;
            }

            _catalogBlob = blobRef;
            _renderMeshEntity = entityManager.CreateEntity();
            entityManager.AddSharedComponentManaged(_renderMeshEntity, renderMeshArray);

            _catalogEntity = entityManager.CreateEntity(typeof(RenderPresentationCatalog), typeof(RenderCatalogVersion));
            var catalogData = new RenderPresentationCatalog
            {
                Blob = _catalogBlob,
                RenderMeshArrayEntity = _renderMeshEntity
            };

            entityManager.SetComponentData(_catalogEntity, catalogData);
            entityManager.SetComponentData(_catalogEntity, new RenderCatalogVersion
            {
                Value = RenderCatalogVersionUtility.Next()
            });

            Debug.Log($"[RenderPresentationCatalogRuntimeBootstrap] Created runtime catalog with {input.Variants?.Length ?? 0} variants.");
        }

        private void OnDestroy()
        {
            if (_catalogBlob.IsCreated)
            {
                _catalogBlob.Dispose();
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }
            var entityManager = world.EntityManager;

            if (_catalogEntity != Entity.Null && entityManager.Exists(_catalogEntity))
            {
                entityManager.DestroyEntity(_catalogEntity);
            }

            if (_renderMeshEntity != Entity.Null && entityManager.Exists(_renderMeshEntity))
            {
                entityManager.DestroyEntity(_renderMeshEntity);
            }

        }
    }
}
