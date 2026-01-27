using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Pure validation helpers for render catalog batching stability and render==sim parity guardrails.
    /// </summary>
    public static class RenderPresentationCatalogValidation
    {
        public struct ThemeCoverageMetrics
        {
            public ushort ThemeId;
            public int SemanticKeyCount;
            public int LodCount;
            public int RequiredKeyCount;
            public int TotalRequiredSlots;
            public int MissingRequiredSlots;
            public float Coverage01;
        }

        public struct RequiredRenderSemanticKey : IBufferElementData
        {
            public ushort Value;
        }

        public static bool TryComputeTheme0RequiredCoverage(
            BlobAssetReference<RenderPresentationCatalogBlob> catalogBlob,
            NativeArray<ushort> requiredSemanticKeys,
            out ThemeCoverageMetrics metrics,
            out FixedString512Bytes error)
        {
            metrics = default;
            error = default;

            if (!catalogBlob.IsCreated)
            {
                error = "Catalog blob is not created.";
                return false;
            }

            ref var catalog = ref catalogBlob.Value;
            if (catalog.Themes.Length == 0 || catalog.ThemeIndexLookup.Length == 0)
            {
                error = "Catalog has no themes or theme lookup.";
                return false;
            }

            const ushort themeId = 0;
            var themeIndex = themeId < catalog.ThemeIndexLookup.Length ? catalog.ThemeIndexLookup[themeId] : -1;
            if (themeIndex < 0 || themeIndex >= catalog.Themes.Length)
            {
                error = "ThemeId=0 is not present in catalog ThemeIndexLookup.";
                return false;
            }

            ref var row = ref catalog.Themes[themeIndex];
            ref var indices = ref row.VariantIndices;

            var semanticCount = Math.Max(1, catalog.SemanticKeyCount);
            var lodCount = Math.Max(1, catalog.LodCount);
            var expected = semanticCount * lodCount;
            if (indices.Length != expected)
            {
                error = $"ThemeId=0 row has VariantIndices.Length={indices.Length}, expected {expected} (semanticCount={semanticCount}, lodCount={lodCount}).";
                return false;
            }

            var requiredCount = requiredSemanticKeys.IsCreated ? requiredSemanticKeys.Length : 0;
            if (requiredCount <= 0)
            {
                error = "Required semantic key universe is empty; cannot validate Theme 0 completeness.";
                return false;
            }

            int missingRequired = 0;
            for (int k = 0; k < requiredCount; k++)
            {
                var semantic = requiredSemanticKeys[k];
                if (semantic < 0 || semantic >= semanticCount)
                {
                    // If a required key is outside the baked semantic range, it's missing by definition.
                    missingRequired += lodCount;
                    continue;
                }

                for (int lod = 0; lod < lodCount; lod++)
                {
                    var flatIndex = lod * semanticCount + semantic;
                    if (flatIndex < 0 || flatIndex >= indices.Length)
                    {
                        missingRequired++;
                        continue;
                    }

                    // Convention: variant index 0 is fallback. For required semantic keys, this is considered missing.
                    if (indices[flatIndex] == 0)
                    {
                        missingRequired++;
                    }
                }
            }

            metrics = new ThemeCoverageMetrics
            {
                ThemeId = row.ThemeId,
                SemanticKeyCount = semanticCount,
                LodCount = lodCount,
                RequiredKeyCount = requiredCount,
                TotalRequiredSlots = requiredCount * lodCount,
                MissingRequiredSlots = missingRequired,
                Coverage01 = (requiredCount * lodCount) > 0
                    ? (float)((requiredCount * lodCount) - missingRequired) / (requiredCount * lodCount)
                    : 1f
            };

            return true;
        }

        public static void ValidateTheme0OrLog(
            EntityManager entityManager,
            in RenderPresentationCatalog catalog,
            bool strict,
            int maxExamplesToPrint = 16)
        {
            if (!catalog.Blob.IsCreated)
                return;

            // Validation requires the game to provide the universe of "required" semantic keys (sparse IDs are expected).
            if (!entityManager.CreateEntityQuery(ComponentType.ReadOnly<RequiredRenderSemanticKey>()).TryGetSingletonBuffer(out DynamicBuffer<RequiredRenderSemanticKey> requiredBuffer))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[PureDOTS.Rendering] Theme 0 completeness validation skipped: RequiredRenderSemanticKey singleton buffer missing.");
#endif
                return;
            }

            var requiredKeys = new NativeArray<ushort>(requiredBuffer.Length, Allocator.Temp);
            try
            {
                for (int i = 0; i < requiredBuffer.Length; i++)
                    requiredKeys[i] = requiredBuffer[i].Value;

                if (!TryComputeTheme0RequiredCoverage(catalog.Blob, requiredKeys, out var metrics, out var error))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError($"[PureDOTS.Rendering] Theme 0 coverage validation failed: {error}");
#endif
                    if (strict)
                        throw new InvalidOperationException(error.ToString());
                    return;
                }

                if (metrics.MissingRequiredSlots <= 0)
                    return;

                // Build a small example list (semantic, lod) for actionable error messages.
                ref var blob = ref catalog.Blob.Value;
                int themeIndex = blob.ThemeIndexLookup[0];
                ref var row = ref blob.Themes[themeIndex];
                ref var indices = ref row.VariantIndices;

                var sb = new FixedString4096Bytes();
                sb.Append($"Theme 0 (ThemeId=0) is missing {metrics.MissingRequiredSlots}/{metrics.TotalRequiredSlots} required semantic mappings (coverage={(metrics.Coverage01 * 100f):0.0}%). Examples: ");
                int printed = 0;
                var semanticCount = metrics.SemanticKeyCount;
                var lodCount = metrics.LodCount;
                for (int k = 0; k < requiredKeys.Length && printed < maxExamplesToPrint; k++)
                {
                    var semantic = requiredKeys[k];
                    for (int lod = 0; lod < lodCount && printed < maxExamplesToPrint; lod++)
                    {
                        var flatIndex = lod * semanticCount + semantic;
                        if (flatIndex < 0 || flatIndex >= indices.Length)
                            continue;

                        if (indices[flatIndex] == 0)
                        {
                            sb.Append($"(semantic={semantic}, lod={lod}) ");
                            printed++;
                        }
                    }
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[PureDOTS.Rendering] Theme 0 completeness violation: {sb}");
#endif
                if (strict)
                    throw new InvalidOperationException(sb.ToString());
            }
            finally
            {
                requiredKeys.Dispose();
            }
        }

        public static bool TryGetRenderMeshArrayCounts(
            EntityManager entityManager,
            Entity renderMeshArrayEntity,
            out int meshCount,
            out int materialCount)
        {
            meshCount = 0;
            materialCount = 0;

            if (renderMeshArrayEntity == Entity.Null || !entityManager.Exists(renderMeshArrayEntity))
                return false;

            if (!entityManager.HasComponent<RenderMeshArray>(renderMeshArrayEntity))
                return false;

            var rma = entityManager.GetSharedComponentManaged<RenderMeshArray>(renderMeshArrayEntity);
            meshCount = rma.MeshReferences?.Length ?? 0;
            materialCount = rma.MaterialReferences?.Length ?? 0;
            return true;
        }
    }
}

