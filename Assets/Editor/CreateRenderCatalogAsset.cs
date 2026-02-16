using UnityEngine;
using UnityEditor;
using PureDOTS.Rendering;
using System.Collections.Generic;
using System.IO;
using Space4X.Presentation;

public class CreateRenderCatalogAsset
{
    private const string ResourcesCatalogPath = "Assets/Resources/Space4XRenderCatalog_v2.asset";
    private const string DataCatalogPath = "Assets/Data/Space4XRenderCatalog_v2.asset";

    [MenuItem("Tools/Space4X/Create Render Catalog Asset")]
    public static void Execute()
    {
        var catalog = ScriptableObject.CreateInstance<RenderPresentationCatalogDefinition>();
        
        // Find Materials
        string matGuid1 = "6d9345776d1d85842b5d4eb03afbd178";
        string matPath1 = AssetDatabase.GUIDToAssetPath(matGuid1);
        Material mat1 = AssetDatabase.LoadAssetAtPath<Material>(matPath1);
        
        string fallbackMatGuid = "eab17db06d3ab6a44a7879edf1d11c2c";
        string fallbackMatPath = AssetDatabase.GUIDToAssetPath(fallbackMatGuid);
        Material fallbackMat = AssetDatabase.LoadAssetAtPath<Material>(fallbackMatPath);
        Material variantMaterial = mat1 != null ? mat1 : fallbackMat;

        if (fallbackMat == null || variantMaterial == null)
        {
            Debug.LogError("[CreateRenderCatalogAsset] Missing fallback material; cannot build render catalog.");
            return;
        }

        // Built-in Meshes
        // We can't easily load built-in meshes by GUID in Editor script without some tricks, 
        // but we can use GetPrimitive or find them in default resources.
        // 10208 = Capsule, 10206 = Cube, 10207 = Sphere
        Mesh capsule = GetBuiltinMesh(PrimitiveType.Capsule);
        Mesh cube = GetBuiltinMesh(PrimitiveType.Cube);
        Mesh sphere = GetBuiltinMesh(PrimitiveType.Sphere);

        catalog.FallbackMesh = capsule;
        catalog.FallbackMaterial = fallbackMat;
        catalog.LodCount = 1;

        var variants = new List<RenderPresentationCatalogDefinition.VariantDefinition>();
        
        // 0: Carrier
        variants.Add(new RenderPresentationCatalogDefinition.VariantDefinition
        {
            Name = "Carrier",
            Mesh = capsule,
            Material = variantMaterial,
            SubMesh = 0,
            BoundsCenter = Vector3.zero,
            BoundsExtents = new Vector3(0.5f, 1f, 0.5f),
            PresenterMask = RenderPresenterMask.Mesh, // Assuming 1 maps to Mesh
            RenderLayer = 0
        });

        // 1: Miner
        variants.Add(new RenderPresentationCatalogDefinition.VariantDefinition
        {
            Name = "Miner",
            Mesh = cube,
            Material = variantMaterial,
            SubMesh = 0,
            BoundsCenter = new Vector3(0.000000059604645f, 0, -0.00000008940697f),
            BoundsExtents = new Vector3(0.50000006f, 1f, 0.5000001f),
            PresenterMask = RenderPresenterMask.Mesh,
            RenderLayer = 0
        });

        // 2: Asteroid
        variants.Add(new RenderPresentationCatalogDefinition.VariantDefinition
        {
            Name = "Asteroid",
            Mesh = sphere,
            Material = variantMaterial,
            SubMesh = 0,
            BoundsCenter = Vector3.zero,
            BoundsExtents = new Vector3(0.5f, 0.5f, 0.5f),
            PresenterMask = RenderPresenterMask.Mesh,
            RenderLayer = 0
        });

        // 3: Projectile
        variants.Add(new RenderPresentationCatalogDefinition.VariantDefinition
        {
            Name = "Projectile",
            Mesh = cube,
            Material = variantMaterial,
            SubMesh = 0,
            BoundsCenter = Vector3.zero,
            BoundsExtents = new Vector3(0.1f, 0.1f, 0.6f),
            PresenterMask = RenderPresenterMask.Mesh,
            RenderLayer = 0
        });

        // 4: FleetImpostor
        variants.Add(new RenderPresentationCatalogDefinition.VariantDefinition
        {
            Name = "FleetImpostor",
            Mesh = capsule,
            Material = variantMaterial,
            SubMesh = 0,
            BoundsCenter = Vector3.zero,
            BoundsExtents = new Vector3(4f, 0.5f, 4f),
            PresenterMask = RenderPresenterMask.Mesh,
            RenderLayer = 0
        });

        // 5: Individual
        variants.Add(new RenderPresentationCatalogDefinition.VariantDefinition
        {
            Name = "Individual",
            Mesh = cube,
            Material = variantMaterial,
            SubMesh = 0,
            BoundsCenter = Vector3.zero,
            BoundsExtents = new Vector3(0.25f, 0.5f, 0.25f),
            PresenterMask = RenderPresenterMask.Mesh,
            RenderLayer = 0
        });

        // 6: StrikeCraft
        variants.Add(new RenderPresentationCatalogDefinition.VariantDefinition
        {
            Name = "StrikeCraft",
            Mesh = capsule,
            Material = variantMaterial,
            SubMesh = 0,
            BoundsCenter = Vector3.zero,
            BoundsExtents = new Vector3(0.35f, 0.2f, 0.6f),
            PresenterMask = RenderPresenterMask.Mesh,
            RenderLayer = 0
        });

        // 7: ResourcePickup
        variants.Add(new RenderPresentationCatalogDefinition.VariantDefinition
        {
            Name = "ResourcePickup",
            Mesh = sphere,
            Material = variantMaterial,
            SubMesh = 0,
            BoundsCenter = Vector3.zero,
            BoundsExtents = new Vector3(0.25f, 0.25f, 0.25f),
            PresenterMask = RenderPresenterMask.Mesh,
            RenderLayer = 0
        });

        // 8: GhostTether
        variants.Add(new RenderPresentationCatalogDefinition.VariantDefinition
        {
            Name = "GhostTether",
            Mesh = cube,
            Material = variantMaterial,
            SubMesh = 0,
            BoundsCenter = Vector3.zero,
            BoundsExtents = new Vector3(0.5f, 0.5f, 0.5f),
            PresenterMask = RenderPresenterMask.Mesh,
            RenderLayer = 0
        });

        catalog.Variants = variants.ToArray();

        var themes = new List<RenderPresentationCatalogDefinition.ThemeDefinition>();
        var defaultTheme = new RenderPresentationCatalogDefinition.ThemeDefinition
        {
            Name = "Default",
            ThemeId = 0,
            SemanticVariants = new RenderPresentationCatalogDefinition.SemanticVariant[]
            {
                new RenderPresentationCatalogDefinition.SemanticVariant
                {
                    SemanticKey = Space4XRenderKeys.Carrier,
                    Lod0Variant = 0,
                    Lod1Variant = -1,
                    Lod2Variant = -1
                },
                new RenderPresentationCatalogDefinition.SemanticVariant
                {
                    SemanticKey = Space4XRenderKeys.Miner,
                    Lod0Variant = 1,
                    Lod1Variant = -1,
                    Lod2Variant = -1
                },
                new RenderPresentationCatalogDefinition.SemanticVariant
                {
                    SemanticKey = Space4XRenderKeys.Asteroid,
                    Lod0Variant = 2,
                    Lod1Variant = -1,
                    Lod2Variant = -1
                },
                new RenderPresentationCatalogDefinition.SemanticVariant
                {
                    SemanticKey = Space4XRenderKeys.Projectile,
                    Lod0Variant = 3,
                    Lod1Variant = -1,
                    Lod2Variant = -1
                },
                new RenderPresentationCatalogDefinition.SemanticVariant
                {
                    SemanticKey = Space4XRenderKeys.FleetImpostor,
                    Lod0Variant = 4,
                    Lod1Variant = -1,
                    Lod2Variant = -1
                },
                new RenderPresentationCatalogDefinition.SemanticVariant
                {
                    SemanticKey = Space4XRenderKeys.Individual,
                    Lod0Variant = 5,
                    Lod1Variant = -1,
                    Lod2Variant = -1
                },
                new RenderPresentationCatalogDefinition.SemanticVariant
                {
                    SemanticKey = Space4XRenderKeys.StrikeCraft,
                    Lod0Variant = 6,
                    Lod1Variant = -1,
                    Lod2Variant = -1
                },
                new RenderPresentationCatalogDefinition.SemanticVariant
                {
                    SemanticKey = Space4XRenderKeys.ResourcePickup,
                    Lod0Variant = 7,
                    Lod1Variant = -1,
                    Lod2Variant = -1
                },
                new RenderPresentationCatalogDefinition.SemanticVariant
                {
                    SemanticKey = Space4XRenderKeys.GhostTether,
                    Lod0Variant = 8,
                    Lod1Variant = -1,
                    Lod2Variant = -1
                }
            }
        };
        themes.Add(defaultTheme);
        catalog.Themes = themes.ToArray();

        SaveCatalogAsset(catalog, ResourcesCatalogPath);
        SaveCatalogAsset(Object.Instantiate(catalog), DataCatalogPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CreateRenderCatalogAsset] Wrote render catalogs to '{ResourcesCatalogPath}' and '{DataCatalogPath}'.");

        // Assign to GameObject
        var go = GameObject.Find("RenderCatalog");
        if (go != null)
        {
            var authoring = go.GetComponent<RenderPresentationCatalogAuthoring>();
            if (authoring != null)
            {
                authoring.CatalogDefinition = catalog;
                EditorUtility.SetDirty(go);
                Debug.Log("Assigned new catalog to RenderCatalog GameObject.");
            }
        }
    }

    private static Mesh GetBuiltinMesh(PrimitiveType type)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
        GameObject.DestroyImmediate(go);
        return mesh;
    }

    private static void SaveCatalogAsset(RenderPresentationCatalogDefinition catalog, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var existing = AssetDatabase.LoadAssetAtPath<RenderPresentationCatalogDefinition>(path);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        AssetDatabase.CreateAsset(catalog, path);
    }
}
