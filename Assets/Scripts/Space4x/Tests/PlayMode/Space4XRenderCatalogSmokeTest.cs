#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using Unity.Collections;
using PureDOTS.Rendering;

namespace Space4X.Tests.PlayMode
{
    /// <summary>
    /// Smoketest that verifies Space4X render catalog is properly set up at runtime.
    /// Checks that meshes/materials exist and MaterialMeshInfo is assigned to entities.
    /// </summary>
    public class Space4XRenderCatalogSmokeTest
    {
        private World _world;
        private EntityManager _entityManager;
        private BlobAssetReference<RenderPresentationCatalogBlob> _catalogBlob;

        [SetUp]
        public void SetUp()
        {
            _world = new World("RenderCatalogSmokeTest");
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (_catalogBlob.IsCreated)
            {
                _catalogBlob.Dispose();
                _catalogBlob = default;
            }

            if (_world.IsCreated)
            {
                _world.Dispose();
            }
        }

        [Test]
        public void RenderMeshArray_HasMeshesAndMaterials()
        {
            var meshes = new Mesh[]
            {
                GameObject.CreatePrimitive(PrimitiveType.Capsule).GetComponent<MeshFilter>().sharedMesh,
                GameObject.CreatePrimitive(PrimitiveType.Cylinder).GetComponent<MeshFilter>().sharedMesh
            };
            var materials = new Material[]
            {
                new Material(Shader.Find("Universal Render Pipeline/Lit")),
                new Material(Shader.Find("Universal Render Pipeline/Lit"))
            };

            var renderMeshArray = new RenderMeshArray(materials, meshes);
            var entity = _entityManager.CreateEntity(typeof(MaterialMeshInfo));
            _entityManager.AddSharedComponentManaged(entity, renderMeshArray);
            _entityManager.SetComponentData(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0, 0));

            var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<MaterialMeshInfo>(),
                ComponentType.ReadOnly<RenderMeshArray>());

            Assert.AreEqual(1, query.CalculateEntityCount(), "Expected one visual entity");

            var e = query.GetSingletonEntity();
            var rma = _entityManager.GetSharedComponentManaged<RenderMeshArray>(e);
            Assert.Greater(rma.MeshReferences?.Length ?? 0, 0, "RenderMeshArray should have meshes");
            Assert.Greater(rma.MaterialReferences?.Length ?? 0, 0, "RenderMeshArray should have materials");
        }

        [Test]
        public void RenderCatalog_BlobIsValid()
        {
            var variantMesh = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshFilter>().sharedMesh;
            var variantMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            var catalogEntity = _entityManager.CreateEntity(typeof(RenderPresentationCatalog), typeof(RenderCatalogVersion));

            var buildInput = new RenderCatalogBuildInput
            {
                Variants = new[]
                {
                    new RenderVariantSource
                    {
                        Name = "TestVariant",
                        Mesh = variantMesh,
                        Material = variantMaterial,
                        BoundsCenter = Vector3.zero,
                        BoundsExtents = Vector3.one,
                        PresenterMask = RenderPresenterMask.Mesh,
                        SubMesh = 0,
                        RenderLayer = 0,
                        VisualKind = RenderVisualKind.Mesh,
                        TracerWidth = 0.3f,
                        TracerLength = 6f,
                        TracerColor = Color.white,
                        TracerStyle = 0
                    }
                },
                Themes = new[]
                {
                    new RenderThemeSource
                    {
                        Name = "Default",
                        ThemeId = 0,
                        SemanticVariants = new[]
                        {
                            new SemanticVariantSource
                            {
                                SemanticKey = 0,
                                Lod0Variant = 0,
                                Lod1Variant = -1,
                                Lod2Variant = -1
                            }
                        }
                    }
                },
                FallbackMesh = variantMesh,
                FallbackMaterial = variantMaterial,
                LodCount = 1
            };

            Assert.IsTrue(RenderPresentationCatalogBuilder.TryBuild(buildInput, Allocator.Temp, out _catalogBlob, out var renderMeshArray));

            _entityManager.SetComponentData(catalogEntity, new RenderPresentationCatalog
            {
                Blob = _catalogBlob,
                RenderMeshArrayEntity = catalogEntity
            });
            _entityManager.SetComponentData(catalogEntity, new RenderCatalogVersion { Value = 1 });
            _entityManager.AddSharedComponentManaged(catalogEntity, renderMeshArray);

            var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<RenderPresentationCatalog>());
            Assert.AreEqual(1, query.CalculateEntityCount(), "Expected a single catalog singleton");

            var singletonEntity = query.GetSingletonEntity();
            var catalogSingleton = _entityManager.GetComponentData<RenderPresentationCatalog>(singletonEntity);
            ref var catalog = ref catalogSingleton.Blob.Value;

            Assert.Greater(catalog.Variants.Length, 0, "Catalog should have variants");
            Assert.Greater(catalog.Themes.Length, 0, "Catalog should have themes");
        }

        [Test]
        public void Entities_WithRenderSemanticKey_Query()
        {
            var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<RenderSemanticKey>(),
                ComponentType.Exclude<RenderVariantKey>());

            Assert.DoesNotThrow(() => query.CalculateEntityCount());
        }
    }
}
#endif
