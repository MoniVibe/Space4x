using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// ScriptableObject describing render variants and themed semantic mappings.
    /// Games author their catalog assets using this definition.
    /// </summary>
    [CreateAssetMenu(menuName = "PureDOTS/Rendering/PresentationCatalog", fileName = "RenderPresentationCatalog")]
    public class RenderPresentationCatalogDefinition : ScriptableObject
    {
        public const int MaxLodCount = 3;

        [SerializeField]
        public VariantDefinition[] Variants = Array.Empty<VariantDefinition>();

        [SerializeField]
        public ThemeDefinition[] Themes = Array.Empty<ThemeDefinition>();

        [Tooltip("Required fallback mesh used when a variant is missing or invalid.")]
        public Mesh FallbackMesh;

        [Tooltip("Required fallback material used when a variant is missing or invalid.")]
        public Material FallbackMaterial;

        [Tooltip("Number of LOD buckets to bake (1 = full detail only, up to 3).")]
        [Range(1, MaxLodCount)]
        public int LodCount = 1;

        [Serializable]
        public class VariantDefinition
        {
            public string Name;
            public Mesh Mesh;
            public Material Material;
            public Vector3 BoundsCenter;
            public Vector3 BoundsExtents;
            public RenderPresenterMask PresenterMask;
            public ushort SubMesh;
            public byte RenderLayer;
            [Tooltip("Declares which visual pipeline this variant is authored for.")]
            public RenderVisualKind VisualKind = RenderVisualKind.Mesh;
            [Tooltip("Default tracer width in meters when VisualKind == Tracer.")]
            public float TracerWidth = 0.3f;
            [Tooltip("Default tracer length in meters when VisualKind == Tracer.")]
            public float TracerLength = 6f;
            [Tooltip("Default tracer color when VisualKind == Tracer.")]
            public Color TracerColor = Color.white;
            [Tooltip("Optional style byte consumed by tracer presenters.")]
            public byte TracerStyle;
        }

        [Serializable]
        public struct ThemeDefinition
        {
            public string Name;
            public ushort ThemeId;
            public SemanticVariant[] SemanticVariants;
        }

        [Serializable]
        public struct SemanticVariant
        {
            public int SemanticKey;

            [FormerlySerializedAs("VariantIndex")]
            public int Lod0Variant;

            public int Lod1Variant;
            public int Lod2Variant;
        }

        public RenderCatalogBuildInput ToBuildInput()
        {
            var variantSources = new RenderVariantSource[Variants?.Length ?? 0];
            for (int i = 0; i < variantSources.Length; i++)
            {
                var variant = Variants[i];
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
                    VisualKind = variant.VisualKind == RenderVisualKind.None ? RenderVisualKind.Mesh : variant.VisualKind,
                    TracerWidth = variant.TracerWidth,
                    TracerLength = variant.TracerLength,
                    TracerColor = variant.TracerColor,
                    TracerStyle = variant.TracerStyle
                };
            }

            var themeSources = new RenderThemeSource[Themes?.Length ?? 0];
            for (int i = 0; i < themeSources.Length; i++)
            {
                var theme = Themes[i];
                var mappings = new SemanticVariantSource[theme.SemanticVariants?.Length ?? 0];
                for (int j = 0; j < mappings.Length; j++)
                {
                    var semanticVariant = theme.SemanticVariants[j];
                    mappings[j] = new SemanticVariantSource
                    {
                        SemanticKey = semanticVariant.SemanticKey,
                        Lod0Variant = semanticVariant.Lod0Variant,
                        Lod1Variant = semanticVariant.Lod1Variant,
                        Lod2Variant = semanticVariant.Lod2Variant
                    };
                }

                themeSources[i] = new RenderThemeSource
                {
                    Name = theme.Name,
                    ThemeId = theme.ThemeId,
                    SemanticVariants = mappings
                };
            }

            return new RenderCatalogBuildInput
            {
                Variants = variantSources,
                Themes = themeSources,
                FallbackMesh = FallbackMesh,
                FallbackMaterial = FallbackMaterial,
                LodCount = Mathf.Clamp(LodCount, 1, MaxLodCount)
            };
        }
    }
}
