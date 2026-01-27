#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using System;

namespace PureDOTS.Tests.Rendering
{
    public class RenderCatalogValidationTests
    {
        [DisableAutoCreation]
        private sealed partial class ValidationWrapperSystem : SystemBase
        {
            private RenderPresentationCatalogValidationSystem _system;

            protected override void OnCreate()
            {
                base.OnCreate();
                _system = new RenderPresentationCatalogValidationSystem();
                _system.OnCreate(ref CheckedStateRef);
            }

            protected override void OnUpdate()
            {
                _system.OnUpdate(ref CheckedStateRef);
            }
        }

        private World _world;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _world = new World("RenderCatalogValidationTests");
            _em = _world.EntityManager;
            World.DefaultGameObjectInjectionWorld = _world;
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated)
            {
                _world.Dispose();
                if (World.DefaultGameObjectInjectionWorld == _world)
                    World.DefaultGameObjectInjectionWorld = null;
            }
        }

        [Test]
        public void Theme0Coverage_ReportsMissingRequiredMappings()
        {
            var input = CreateCatalogInput(semanticMaxKey: 3, mapAllSemanticsInTheme0: false);
            Assert.IsTrue(RenderPresentationCatalogBuilder.TryBuild(input, Allocator.Temp, out var blob, out var renderMeshArray));
            Assert.IsTrue(blob.IsCreated);

            using var required = new NativeArray<ushort>(new ushort[] { 0, 1, 2, 3 }, Allocator.Temp);
            Assert.IsTrue(RenderPresentationCatalogValidation.TryComputeTheme0RequiredCoverage(blob, required, out var metrics, out var error), error.ToString());
            Assert.Greater(metrics.MissingRequiredSlots, 0);

            blob.Dispose();
        }

        [Test]
        public void Theme0Coverage_IsCompleteWhenAllRequiredSemanticsMapped()
        {
            var input = CreateCatalogInput(semanticMaxKey: 7, mapAllSemanticsInTheme0: true);
            Assert.IsTrue(RenderPresentationCatalogBuilder.TryBuild(input, Allocator.Temp, out var blob, out var renderMeshArray));
            Assert.IsTrue(blob.IsCreated);

            using var required = new NativeArray<ushort>(new ushort[] { 0, 1, 2, 3, 4, 5, 6, 7 }, Allocator.Temp);
            Assert.IsTrue(RenderPresentationCatalogValidation.TryComputeTheme0RequiredCoverage(blob, required, out var metrics, out var error), error.ToString());
            Assert.AreEqual(0, metrics.MissingRequiredSlots);
            Assert.AreEqual(1f, metrics.Coverage01, 1e-6f);

            blob.Dispose();
        }

        [Test]
        public void StrictValidation_ThrowsWhenTheme0MissingRequiredMappings()
        {
            var input = CreateCatalogInput(semanticMaxKey: 3, mapAllSemanticsInTheme0: false);
            Assert.IsTrue(RenderPresentationCatalogBuilder.TryBuild(input, Allocator.Temp, out var blob, out var renderMeshArray));
            Assert.IsTrue(blob.IsCreated);

            // Required semantic universe (matches the tiny input we built).
            var requiredEntity = _em.CreateEntity();
            var required = _em.AddBuffer<RenderPresentationCatalogValidation.RequiredRenderSemanticKey>(requiredEntity);
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = 0 });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = 1 });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = 2 });
            required.Add(new RenderPresentationCatalogValidation.RequiredRenderSemanticKey { Value = 3 });

            // Strict settings singleton.
            var settingsEntity = _em.CreateEntity(typeof(RenderCatalogValidationSettings));
            _em.SetComponentData(settingsEntity, new RenderCatalogValidationSettings { Strict = 1 });

            // Catalog singleton.
            var rmaEntity = _em.CreateEntity();
            _em.AddSharedComponentManaged(rmaEntity, renderMeshArray);

            var catalogEntity = _em.CreateEntity(typeof(RenderPresentationCatalog), typeof(RenderCatalogVersion));
            _em.SetComponentData(catalogEntity, new RenderPresentationCatalog
            {
                Blob = blob,
                RenderMeshArrayEntity = rmaEntity
            });
            _em.SetComponentData(catalogEntity, new RenderCatalogVersion { Value = 1 });

            var system = _world.GetOrCreateSystemManaged<ValidationWrapperSystem>();

            Assert.Throws<InvalidOperationException>(() => system.Update());

            blob.Dispose();
        }

        private static RenderCatalogBuildInput CreateCatalogInput(int semanticMaxKey, bool mapAllSemanticsInTheme0)
        {
            // Keep shaders robust in edit/test environments.
            var shader = Shader.Find("Hidden/InternalErrorShader");
            var fallbackMat = new Material(shader);
            var fallbackMesh = new Mesh();

            // Provide a few concrete variants beyond fallback.
            var variants = new[]
            {
                new RenderVariantSource { Name = "V1", Mesh = fallbackMesh, Material = fallbackMat, VisualKind = RenderVisualKind.Mesh },
                new RenderVariantSource { Name = "V2", Mesh = fallbackMesh, Material = fallbackMat, VisualKind = RenderVisualKind.Mesh },
            };

            SemanticVariantSource[] mappings;
            if (mapAllSemanticsInTheme0)
            {
                mappings = new SemanticVariantSource[semanticMaxKey + 1];
                for (int s = 0; s <= semanticMaxKey; s++)
                {
                    mappings[s] = new SemanticVariantSource
                    {
                        SemanticKey = s,
                        Lod0Variant = 0, // maps to slot 1 (V1)
                        Lod1Variant = 0,
                        Lod2Variant = 0
                    };
                }
            }
            else
            {
                // Intentionally omit at least one semantic key.
                mappings = new[]
                {
                    new SemanticVariantSource { SemanticKey = 0, Lod0Variant = 0, Lod1Variant = 0, Lod2Variant = 0 },
                    new SemanticVariantSource { SemanticKey = 1, Lod0Variant = 0, Lod1Variant = 0, Lod2Variant = 0 },
                    // SemanticKey 2 missing
                    new SemanticVariantSource { SemanticKey = 3, Lod0Variant = 1, Lod1Variant = 1, Lod2Variant = 1 },
                };
            }

            var themes = new[]
            {
                new RenderThemeSource
                {
                    Name = "Theme0",
                    ThemeId = 0,
                    SemanticVariants = mappings
                }
            };

            return new RenderCatalogBuildInput
            {
                Variants = variants,
                Themes = themes,
                FallbackMesh = fallbackMesh,
                FallbackMaterial = fallbackMat,
                LodCount = 1
            };
        }
    }
}
#endif

