using PureDOTS.Rendering;
using Space4X.Rendering;
using Space4X.Rendering.Catalog;
using UnityEditor;
using UnityEngine;

public class ConfigureRenderCatalog
{
    [MenuItem("Space4X/Configure Render Catalog")]
    public static void Configure()
    {
        var go = GameObject.Find("RenderCatalog");
        if (go == null)
        {
            go = new GameObject("RenderCatalog");
            Undo.RegisterCreatedObjectUndo(go, "Create RenderCatalog");
        }

        var authoring = go.GetComponent<RenderCatalogAuthoring>();
        if (authoring == null)
        {
            authoring = Undo.AddComponent<RenderCatalogAuthoring>(go);
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DebugRed.mat");
        if (material == null)
        {
            // Create a default material if DebugRed doesn't exist
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = Color.white;
            AssetDatabase.CreateAsset(material, "Assets/Materials/DebugWhite.mat");
        }

        // Ensure we have a catalog asset
        var catalog = authoring.CatalogDefinition;
        if (catalog == null)
        {
            // Try to find an existing catalog asset in the project
            string[] guids = AssetDatabase.FindAssets("t:Space4XRenderCatalogDefinition");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                catalog = AssetDatabase.LoadAssetAtPath<Space4XRenderCatalogDefinition>(path);
            }

            // If none found, create a new one at a safe default path
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<Space4XRenderCatalogDefinition>();
                AssetDatabase.CreateAsset(catalog, "Assets/Space4XRenderCatalog.asset");
                AssetDatabase.SaveAssets();
            }

            authoring.CatalogDefinition = catalog;
        }

        // Build Variants array for the catalog
        var variants = new Space4XRenderCatalogDefinition.Variant[3];

        // Carrier (Capsule)
        {
            Mesh mesh = GetPrimitiveMesh(PrimitiveType.Capsule);
            var bounds = mesh.bounds;

            variants[0] = new Space4XRenderCatalogDefinition.Variant
            {
                Name          = "Carrier",
                Mesh          = mesh,
                Material      = material,
                BoundsCenter  = bounds.center,
                BoundsExtents = bounds.extents,
                PresenterMask = PureDOTS.Rendering.RenderPresenterMask.Mesh,
                RenderLayer   = 0
            };
        }

        // Miner (Cylinder)
        {
            Mesh mesh = GetPrimitiveMesh(PrimitiveType.Cylinder);
            var bounds = mesh.bounds;

            variants[1] = new Space4XRenderCatalogDefinition.Variant
            {
                Name          = "Miner",
                Mesh          = mesh,
                Material      = material,
                BoundsCenter  = bounds.center,
                BoundsExtents = bounds.extents,
                PresenterMask = PureDOTS.Rendering.RenderPresenterMask.Mesh,
                RenderLayer   = 0
            };
        }

        // Asteroid (Sphere)
        {
            Mesh mesh = GetPrimitiveMesh(PrimitiveType.Sphere);
            var bounds = mesh.bounds;

            variants[2] = new Space4XRenderCatalogDefinition.Variant
            {
                Name          = "Asteroid",
                Mesh          = mesh,
                Material      = material,
                BoundsCenter  = bounds.center,
                BoundsExtents = bounds.extents,
                PresenterMask = PureDOTS.Rendering.RenderPresenterMask.Mesh,
                RenderLayer   = 0
            };
        }

        catalog.Variants = variants;
        catalog.Themes = new[]
        {
            new Space4XRenderCatalogDefinition.Theme
            {
                Name = "Default",
                ThemeId = 0,
                SemanticVariants = new[]
                {
                    new Space4XRenderCatalogDefinition.SemanticVariant
                    {
                        SemanticKey = Space4XRenderKeys.Carrier,
                        Lod0Variant = 0,
                        Lod1Variant = 0,
                        Lod2Variant = 0
                    },
                    new Space4XRenderCatalogDefinition.SemanticVariant
                    {
                        SemanticKey = Space4XRenderKeys.Miner,
                        Lod0Variant = 1,
                        Lod1Variant = 1,
                        Lod2Variant = 1
                    },
                    new Space4XRenderCatalogDefinition.SemanticVariant
                    {
                        SemanticKey = Space4XRenderKeys.Asteroid,
                        Lod0Variant = 2,
                        Lod1Variant = 2,
                        Lod2Variant = 2
                    }
                }
            }
        };

        catalog.FallbackMaterial = material;
        catalog.FallbackMesh = GetPrimitiveMesh(PrimitiveType.Cube);

        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(authoring);
        AssetDatabase.SaveAssets();

        Debug.Log("[Space4X] RenderCatalog configured.");
    }

    private static Mesh GetPrimitiveMesh(PrimitiveType type)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(primitive);
        return mesh;
    }
}
