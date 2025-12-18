using System;
using PureDOTS.Rendering;
using UnityEngine;

namespace Space4X.Rendering.Catalog
{
    [CreateAssetMenu(fileName = "Space4XRenderCatalog", menuName = "Space4X/Rendering/RenderCatalog")]
    public class Space4XRenderCatalogDefinition : ScriptableObject
    {
        public const int MaxLodCount = 3;

        [Serializable]
        public class Variant
        {
            public string Name;
            public Mesh Mesh;
            public Material Material;
            public ushort SubMesh;
            public Vector3 BoundsCenter;
            public Vector3 BoundsExtents;
            public RenderPresenterMask PresenterMask;
            public byte RenderLayer;
            public RenderVisualKind VisualKind = RenderVisualKind.Mesh;
            public float TracerWidth = 0.3f;
            public float TracerLength = 6f;
            public Color TracerColor = Color.white;
            public byte TracerStyle;
        }

        [Serializable]
        public struct Theme
        {
            public string Name;
            public ushort ThemeId;
            public SemanticVariant[] SemanticVariants;
        }

        [Serializable]
        public struct SemanticVariant
        {
            public int SemanticKey;
            public int Lod0Variant;
            public int Lod1Variant;
            public int Lod2Variant;
        }

        public Variant[] Variants = Array.Empty<Variant>();
        public Theme[] Themes = Array.Empty<Theme>();
        public Mesh FallbackMesh;
        public Material FallbackMaterial;
    }
}
