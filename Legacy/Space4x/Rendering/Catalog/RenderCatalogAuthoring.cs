using System;
using PureDOTS.Rendering;
using Space4X.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityDebug = UnityEngine.Debug;
#endif

namespace Space4X.Rendering.Catalog
{
    using Debug = UnityEngine.Debug;

    [DisallowMultipleComponent]
    public sealed partial class RenderCatalogAuthoring : MonoBehaviour
    {
        public Space4XRenderCatalogDefinition CatalogDefinition;

        [Header("Fallback Overrides")]
        [SerializeField] private Material fallbackMaterial;
        [SerializeField] private Mesh fallbackMesh;

        private BlobAssetReference<RenderPresentationCatalogBlob> _runtimeCatalogRef;
        private Entity _runtimeCatalogEntity = Entity.Null;
        private World _world;
        private bool _ownsCatalogEntity;

        public Material EffectiveFallbackMaterial =>
            CatalogDefinition != null && CatalogDefinition.FallbackMaterial != null
                ? CatalogDefinition.FallbackMaterial
                : fallbackMaterial;

        public Mesh EffectiveFallbackMesh =>
            CatalogDefinition != null && CatalogDefinition.FallbackMesh != null
                ? CatalogDefinition.FallbackMesh
                : fallbackMesh;

#if UNITY_EDITOR
        private const string DefaultFallbackMaterialPath = "Assets/Shared/Rendering/Materials/M_EntitiesFallback_URP.mat";
        private const string DefaultFallbackMeshPath = "Assets/Shared/Rendering/Meshes/M_FallbackURPMesh.asset";

        private void Reset()
        {
            TryAssignDefaultFallbacks();
        }

        private void OnValidate()
        {
            TryAssignDefaultFallbacks();
        }

        private void TryAssignDefaultFallbacks()
        {
            if (fallbackMaterial == null)
            {
                fallbackMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultFallbackMaterialPath);
            }

            if (fallbackMesh == null)
            {
                fallbackMesh = AssetDatabase.LoadAssetAtPath<Mesh>(DefaultFallbackMeshPath);
                if (fallbackMesh == null)
                {
                    fallbackMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                }
            }
        }
#endif

        private void Start()
        {
            if (!Application.isPlaying)
                return;
            if (CatalogDefinition == null)
                return;
            if (!ValidateFallbackAssets())
                return;

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
                return;

            Bootstrap(_world.EntityManager);
        }

        private void OnDisable()
        {
            CleanupRuntimeCatalog();
        }

        private void OnDestroy()
        {
            CleanupRuntimeCatalog();
        }

        private void Bootstrap(EntityManager entityManager)
        {
            if (_runtimeCatalogRef.IsCreated ||
                (_runtimeCatalogEntity != Entity.Null && entityManager.Exists(_runtimeCatalogEntity)))
            {
                return;
            }

            using var existingCatalogQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RenderPresentationCatalog>());
            if (!existingCatalogQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (!TryBuildCatalogAssets(CatalogDefinition, EffectiveFallbackMaterial, EffectiveFallbackMesh,
                    out var renderMeshArray, out var catalogBlob))
            {
                return;
            }

            _runtimeCatalogRef = catalogBlob;
            _runtimeCatalogEntity = entityManager.CreateEntity();
            _ownsCatalogEntity = true;

            entityManager.AddComponentData(_runtimeCatalogEntity, new RenderPresentationCatalog
            {
                Blob = _runtimeCatalogRef,
                RenderMeshArrayEntity = _runtimeCatalogEntity
            });

            entityManager.AddComponentData(_runtimeCatalogEntity, new RenderCatalogVersion
            {
                Value = RenderCatalogVersionUtility.Next()
            });

            entityManager.AddSharedComponentManaged(_runtimeCatalogEntity, renderMeshArray);
            var meshCount = renderMeshArray.MeshReferences != null ? renderMeshArray.MeshReferences.Length : 0;
            LogInfo($"[RenderCatalogAuthoring] Bootstrapped runtime catalog with {meshCount} variants.");
        }

        private void CleanupRuntimeCatalog()
        {
            if (_ownsCatalogEntity &&
                _world != null &&
                _world.IsCreated &&
                _runtimeCatalogEntity != Entity.Null &&
                _world.EntityManager.Exists(_runtimeCatalogEntity))
            {
                _world.EntityManager.DestroyEntity(_runtimeCatalogEntity);
            }

            _runtimeCatalogEntity = Entity.Null;
            _ownsCatalogEntity = false;

            if (_runtimeCatalogRef.IsCreated)
            {
                _runtimeCatalogRef.Dispose();
                _runtimeCatalogRef = default;
            }
        }

        private bool ValidateFallbackAssets()
        {
            bool valid = true;
            if (EffectiveFallbackMaterial == null)
            {
                UnityDebug.LogError("[RenderCatalogAuthoring] Fallback material is null. Assign M_EntitiesFallback_URP.");
                valid = false;
            }

            if (EffectiveFallbackMesh == null)
            {
                UnityDebug.LogError("[RenderCatalogAuthoring] Fallback mesh is null.");
                valid = false;
            }

            return valid;
        }

        private static void LogInfo(string message)
        {
#if UNITY_EDITOR
            UnityDebug.Log(message);
#else
            if (UnityDebug.isDebugBuild && !Application.isBatchMode)
            {
                UnityDebug.Log(message);
            }
#endif
        }

        internal static bool TryBuildCatalogAssets(
            Space4XRenderCatalogDefinition catalogDefinition,
            Material fallbackMaterial,
            Mesh fallbackMesh,
            out RenderMeshArray renderMeshArray,
            out BlobAssetReference<RenderPresentationCatalogBlob> catalogBlob)
        {
            renderMeshArray = default;
            catalogBlob = default;

            if (catalogDefinition == null)
            {
                UnityDebug.LogError("[RenderCatalogAuthoring] CatalogDefinition is null.");
                return false;
            }

            if (fallbackMaterial == null || fallbackMesh == null)
            {
                UnityDebug.LogError("[RenderCatalogAuthoring] Cannot build catalog without fallback assets.");
                return false;
            }

            var variantDefinitions = catalogDefinition.Variants ?? Array.Empty<Space4XRenderCatalogDefinition.Variant>();
            var themeDefinitions = catalogDefinition.Themes ?? Array.Empty<Space4XRenderCatalogDefinition.Theme>();
            if (themeDefinitions.Length == 0)
            {
                UnityDebug.LogError("[RenderCatalogAuthoring] Catalog must define at least one theme.");
                return false;
            }

            var variantSources = new RenderVariantSource[variantDefinitions.Length];
            for (int i = 0; i < variantDefinitions.Length; i++)
            {
                var variant = variantDefinitions[i];
                variantSources[i] = new RenderVariantSource
                {
                    Name = variant.Name,
                    Mesh = variant.Mesh,
                    Material = variant.Material,
                    BoundsCenter = variant.BoundsCenter,
                    BoundsExtents = variant.BoundsExtents,
                    PresenterMask = variant.PresenterMask,
                    SubMesh = variant.SubMesh,
                    RenderLayer = variant.RenderLayer,
                    VisualKind = variant.VisualKind,
                    TracerWidth = variant.TracerWidth,
                    TracerLength = variant.TracerLength,
                    TracerColor = variant.TracerColor,
                    TracerStyle = variant.TracerStyle
                };
            }

            var themeSources = new RenderThemeSource[themeDefinitions.Length];
            for (int i = 0; i < themeDefinitions.Length; i++)
            {
                var theme = themeDefinitions[i];
                var semanticMappings = theme.SemanticVariants ?? Array.Empty<Space4XRenderCatalogDefinition.SemanticVariant>();
                var mappings = new SemanticVariantSource[semanticMappings.Length];
                for (int j = 0; j < semanticMappings.Length; j++)
                {
                    var mapping = semanticMappings[j];
                    mappings[j] = new SemanticVariantSource
                    {
                        SemanticKey = mapping.SemanticKey,
                        Lod0Variant = mapping.Lod0Variant,
                        Lod1Variant = mapping.Lod1Variant,
                        Lod2Variant = mapping.Lod2Variant
                    };
                }

                themeSources[i] = new RenderThemeSource
                {
                    Name = theme.Name,
                    ThemeId = theme.ThemeId,
                    SemanticVariants = mappings
                };
            }

            var buildInput = new RenderCatalogBuildInput
            {
                Variants = variantSources,
                Themes = themeSources,
                FallbackMesh = fallbackMesh,
                FallbackMaterial = fallbackMaterial,
                LodCount = Space4XRenderCatalogDefinition.MaxLodCount
            };

            if (!RenderPresentationCatalogBuilder.TryBuild(buildInput, Allocator.Temp, out var blobRef, out renderMeshArray))
            {
                return false;
            }

            catalogBlob = blobRef;
            return true;
        }
    }

    public sealed class RenderCatalogBaker : Baker<RenderCatalogAuthoring>
    {
        public override void Bake(RenderCatalogAuthoring authoring)
        {
            if (!RenderCatalogAuthoring.TryBuildCatalogAssets(authoring.CatalogDefinition,
                    authoring.EffectiveFallbackMaterial,
                    authoring.EffectiveFallbackMesh,
                    out var renderMeshArray,
                    out var catalogBlob))
            {
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new RenderPresentationCatalog
            {
                Blob = catalogBlob,
                RenderMeshArrayEntity = entity
            });

            AddComponent(entity, new RenderCatalogVersion
            {
                Value = RenderCatalogVersionUtility.Next()
            });

            AddSharedComponentManaged(entity, renderMeshArray);
        }
    }
}
