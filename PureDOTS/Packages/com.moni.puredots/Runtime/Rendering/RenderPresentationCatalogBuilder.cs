using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace PureDOTS.Rendering
{
    public struct RenderVariantSource
    {
        public string Name;
        public Mesh Mesh;
        public Material Material;
        public Vector3 BoundsCenter;
        public Vector3 BoundsExtents;
        public RenderPresenterMask PresenterMask;
        public ushort SubMesh;
        public byte RenderLayer;
        public RenderVisualKind VisualKind;
        public float TracerWidth;
        public float TracerLength;
        public Color TracerColor;
        public byte TracerStyle;
    }

    public struct RenderThemeSource
    {
        public string Name;
        public ushort ThemeId;
        public SemanticVariantSource[] SemanticVariants;
    }

    public struct SemanticVariantSource
    {
        public int SemanticKey;
        public int Lod0Variant;
        public int Lod1Variant;
        public int Lod2Variant;
    }

    public struct RenderCatalogBuildInput
    {
        public RenderVariantSource[] Variants;
        public RenderThemeSource[] Themes;
        public Mesh FallbackMesh;
        public Material FallbackMaterial;
        public int LodCount;
    }

    public static class RenderPresentationCatalogBuilder
    {
        public static bool TryBuild(
            in RenderCatalogBuildInput input,
            Allocator allocator,
            out BlobAssetReference<RenderPresentationCatalogBlob> blob,
            out RenderMeshArray renderMeshArray)
        {
            blob = default;
            renderMeshArray = default;

            if (input.FallbackMesh == null || input.FallbackMaterial == null)
            {
                Debug.LogError("[RenderPresentationCatalogBuilder] Fallback mesh/material are required.");
                return false;
            }

            var variantSources = input.Variants ?? Array.Empty<RenderVariantSource>();
            var themeSources = input.Themes ?? Array.Empty<RenderThemeSource>();
            if (themeSources.Length == 0)
            {
                Debug.LogError("[RenderPresentationCatalogBuilder] At least one theme definition is required.");
                return false;
            }

            var variantCount = math.max(1, variantSources.Length + 1);
            using var builder = new BlobBuilder(allocator);
            ref var root = ref builder.ConstructRoot<RenderPresentationCatalogBlob>();
            var variants = builder.Allocate(ref root.Variants, variantCount);

            var materialList = new List<Material>(variantCount);
            var meshList = new List<Mesh>(variantCount);
            var materialIndices = new Dictionary<Material, ushort>();
            var meshIndices = new Dictionary<Mesh, ushort>();
            var fallbackMaterial = input.FallbackMaterial;
            var fallbackMesh = input.FallbackMesh;

            ushort GetOrAddMaterial(Material mat)
            {
                if (mat == null)
                    mat = fallbackMaterial;
                if (materialIndices.TryGetValue(mat, out var existing))
                    return existing;
                if (materialList.Count >= ushort.MaxValue)
                {
                    Debug.LogError("[RenderPresentationCatalogBuilder] Exceeded maximum unique materials (65535).");
                    return 0;
                }
                var index = (ushort)materialList.Count;
                materialIndices.Add(mat, index);
                materialList.Add(mat);
                return index;
            }

            ushort GetOrAddMesh(Mesh mesh)
            {
                if (mesh == null)
                    mesh = fallbackMesh;
                if (meshIndices.TryGetValue(mesh, out var existing))
                    return existing;
                if (meshList.Count >= ushort.MaxValue)
                {
                    Debug.LogError("[RenderPresentationCatalogBuilder] Exceeded maximum unique meshes (65535).");
                    return 0;
                }
                var index = (ushort)meshList.Count;
                meshIndices.Add(mesh, index);
                meshList.Add(mesh);
                return index;
            }

            // slot 0 is fallback
            var fallbackMaterialIndex = GetOrAddMaterial(fallbackMaterial);
            var fallbackMeshIndex = GetOrAddMesh(fallbackMesh);
            variants[0] = CreateVariantSource(
                fallbackMaterial,
                fallbackMesh,
                Vector3.zero,
                Vector3.one * 0.5f,
                RenderPresenterMask.Mesh | RenderPresenterMask.Sprite | RenderPresenterMask.Debug,
                0,
                0,
                fallbackMaterialIndex,
                fallbackMeshIndex,
                RenderVisualKind.Mesh,
                0.5f,
                0.5f,
                Color.white,
                0);

            for (int i = 0; i < variantSources.Length; i++)
            {
                var source = variantSources[i];
                var mesh = source.Mesh != null ? source.Mesh : input.FallbackMesh;
                var material = source.Material != null ? source.Material : input.FallbackMaterial;
                var boundsCenter = source.BoundsCenter;
                var boundsExtents = source.BoundsExtents;
                if (boundsExtents == Vector3.zero && mesh != null)
                {
                    boundsCenter = mesh.bounds.center;
                    boundsExtents = mesh.bounds.extents;
                }
                if (boundsExtents == Vector3.zero)
                {
                    boundsExtents = Vector3.one * 0.5f;
                }

                var visualKind = source.VisualKind == RenderVisualKind.None ? RenderVisualKind.Mesh : source.VisualKind;
                var mask = source.PresenterMask == RenderPresenterMask.None
                    ? ResolveDefaultMask(visualKind)
                    : source.PresenterMask;
                if (visualKind == RenderVisualKind.Tracer)
                {
                    mask |= RenderPresenterMask.Tracer;
                }

                var tracerWidth = math.max(source.TracerWidth, 0.01f);
                var tracerLength = math.max(source.TracerLength, 0.01f);
                var slot = (ushort)(i + 1);
                var materialIndex = GetOrAddMaterial(material);
                var meshIndex = GetOrAddMesh(mesh);
                variants[slot] = CreateVariantSource(
                    material,
                    mesh,
                    boundsCenter,
                    boundsExtents,
                    mask,
                    source.SubMesh,
                    source.RenderLayer,
                    materialIndex,
                    meshIndex,
                    visualKind,
                    tracerWidth,
                    tracerLength,
                    source.TracerColor,
                    source.TracerStyle);
            }

            int maxThemeId = 0;
            int maxSemanticKey = 0;
            foreach (var theme in themeSources)
            {
                maxThemeId = math.max(maxThemeId, theme.ThemeId);
                var mappings = theme.SemanticVariants;
                if (mappings == null)
                    continue;
                foreach (var mapping in mappings)
                {
                    maxSemanticKey = math.max(maxSemanticKey, mapping.SemanticKey);
                }
            }

            var semanticCount = math.max(1, maxSemanticKey + 1);
            var lodCount = math.clamp(input.LodCount <= 0 ? 1 : input.LodCount, 1, RenderPresentationCatalogDefinition.MaxLodCount);
            root.SemanticKeyCount = semanticCount;
            root.LodCount = lodCount;
            root.DefaultThemeIndex = 0;

            var themeRows = builder.Allocate(ref root.Themes, themeSources.Length);
            for (int themeIndex = 0; themeIndex < themeSources.Length; themeIndex++)
            {
                var theme = themeSources[themeIndex];
                themeRows[themeIndex].ThemeId = theme.ThemeId;
                var variantIndices = builder.Allocate(ref themeRows[themeIndex].VariantIndices, semanticCount * lodCount);
                for (int i = 0; i < variantIndices.Length; i++)
                {
                    // Default to fallback variant 0 for unmapped semantics.
                    // Completeness enforcement is performed against an explicit "required semantic key universe" (per game),
                    // not against the full dense 0..maxSemanticKey range.
                    variantIndices[i] = 0;
                }

                var mappings = theme.SemanticVariants;
                if (mappings == null)
                    continue;

                foreach (var mapping in mappings)
                {
                    if (mapping.SemanticKey < 0 || mapping.SemanticKey >= semanticCount)
                        continue;
                    AssignVariantIndex(ref variantIndices, semanticCount, lodCount, mapping.SemanticKey, 0, mapping.Lod0Variant, variantCount);
                    AssignVariantIndex(ref variantIndices, semanticCount, lodCount, mapping.SemanticKey, 1, mapping.Lod1Variant, variantCount);
                    AssignVariantIndex(ref variantIndices, semanticCount, lodCount, mapping.SemanticKey, 2, mapping.Lod2Variant, variantCount);
                }
            }

            var lookupLength = math.max(maxThemeId + 1, 1);
            var lookup = builder.Allocate(ref root.ThemeIndexLookup, lookupLength);
            for (int i = 0; i < lookup.Length; i++)
            {
                lookup[i] = -1;
            }

            for (int themeIndex = 0; themeIndex < themeSources.Length; themeIndex++)
            {
                var themeId = themeSources[themeIndex].ThemeId;
                if (themeId >= lookup.Length || themeId < 0)
                    continue;
                lookup[themeId] = themeIndex;
            }

            blob = builder.CreateBlobAssetReference<RenderPresentationCatalogBlob>(Allocator.Persistent);
            renderMeshArray = new RenderMeshArray(materialList.ToArray(), meshList.ToArray());
            return true;
        }

        private static RenderPresenterMask ResolveDefaultMask(RenderVisualKind visualKind)
        {
            switch (visualKind)
            {
                case RenderVisualKind.Sprite:
                    return RenderPresenterMask.Sprite;
                case RenderVisualKind.Tracer:
                    return RenderPresenterMask.Tracer;
                case RenderVisualKind.Debug:
                    return RenderPresenterMask.Debug;
                default:
                    return RenderPresenterMask.Mesh;
            }
        }

        private static RenderVariantData CreateVariantSource(
            Material material,
            Mesh mesh,
            Vector3 boundsCenter,
            Vector3 boundsExtents,
            RenderPresenterMask presenterMask,
            ushort subMesh,
            byte renderLayer,
            ushort materialIndex,
            ushort meshIndex,
            RenderVisualKind visualKind,
            float tracerWidth,
            float tracerLength,
            Color tracerColor,
            byte tracerStyle)
        {
            var safeBounds = boundsExtents == Vector3.zero ? Vector3.one * 0.5f : boundsExtents;
            var safeMask = presenterMask == RenderPresenterMask.None ? RenderPresenterMask.Mesh : presenterMask;
            var safeWidth = math.max(tracerWidth, 0.01f);
            var safeLength = math.max(tracerLength, 0.01f);
            var colorLinear = tracerColor.linear;
            var safeColor = new float4(colorLinear.r, colorLinear.g, colorLinear.b, colorLinear.a);

            return new RenderVariantData
            {
                MaterialIndex = materialIndex,
                MeshIndex = meshIndex,
                SubMesh = subMesh,
                RenderLayer = renderLayer,
                PresenterMask = safeMask,
                BoundsCenter = (float3)boundsCenter,
                BoundsExtents = (float3)safeBounds,
                VisualKind = visualKind == RenderVisualKind.None ? RenderVisualKind.Mesh : visualKind,
                TracerWidth = safeWidth,
                TracerLength = safeLength,
                TracerColor = safeColor,
                TracerStyle = tracerStyle
            };
        }

        private static void AssignVariantIndex(
            ref BlobBuilderArray<int> variantIndices,
            int semanticCount,
            int lodCount,
            int semanticKey,
            int lod,
            int requestedVariantIndex,
            int variantCount)
        {
            if (lodCount <= 0 || lod < 0 || lod >= lodCount)
                return;

            int flatIndex = lod * semanticCount + semanticKey;
            if (flatIndex < 0 || flatIndex >= variantIndices.Length)
                return;

            var slot = requestedVariantIndex + 1;
            if (requestedVariantIndex < 0 || slot >= variantCount)
            {
                slot = 0;
            }

            variantIndices[flatIndex] = slot;
        }
    }
}
