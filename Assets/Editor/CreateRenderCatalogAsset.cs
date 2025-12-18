using UnityEngine;
using UnityEditor;
using PureDOTS.Rendering;
using System.Collections.Generic;

public class CreateRenderCatalogAsset
{
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
            Material = mat1,
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
            Material = mat1,
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
            Material = mat1,
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
            Material = mat1,
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
            Material = mat1,
            SubMesh = 0,
            BoundsCenter = Vector3.zero,
            BoundsExtents = new Vector3(4f, 0.5f, 4f),
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
                new RenderPresentationCatalogDefinition.SemanticVariant { SemanticKey = 200, Lod0Variant = 0, Lod1Variant = 0, Lod2Variant = 0 },
                new RenderPresentationCatalogDefinition.SemanticVariant { SemanticKey = 210, Lod0Variant = 1, Lod1Variant = 1, Lod2Variant = 1 },
                new RenderPresentationCatalogDefinition.SemanticVariant { SemanticKey = 220, Lod0Variant = 2, Lod1Variant = 2, Lod2Variant = 2 },
                new RenderPresentationCatalogDefinition.SemanticVariant { SemanticKey = 230, Lod0Variant = 3, Lod1Variant = 3, Lod2Variant = 3 },
                new RenderPresentationCatalogDefinition.SemanticVariant { SemanticKey = 240, Lod0Variant = 4, Lod1Variant = 4, Lod2Variant = 4 }
            }
        };
        themes.Add(defaultTheme);
        catalog.Themes = themes.ToArray();

        string path = "Assets/Data/Space4XRenderCatalog_v2.asset";
        AssetDatabase.CreateAsset(catalog, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created catalog asset at {path}");

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
}
