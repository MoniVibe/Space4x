using UnityEngine;
using UnityEditor;
using Space4X.Rendering.Catalog;
using Space4X.Rendering;

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

        // Build Entries array for the catalog
        var entries = new Space4XRenderCatalogDefinition.Entry[3];

        // Carrier (Capsule)
        {
            Mesh mesh = GetPrimitiveMesh(PrimitiveType.Capsule);
            var bounds = mesh.bounds;

            entries[0] = new Space4XRenderCatalogDefinition.Entry
            {
                ArchetypeId   = Space4XRenderKeys.Carrier,
                Mesh          = mesh,
                Material      = material,
                BoundsCenter  = bounds.center,
                BoundsExtents = bounds.extents
            };
        }

        // Miner (Cylinder)
        {
            Mesh mesh = GetPrimitiveMesh(PrimitiveType.Cylinder);
            var bounds = mesh.bounds;

            entries[1] = new Space4XRenderCatalogDefinition.Entry
            {
                ArchetypeId   = Space4XRenderKeys.Miner,
                Mesh          = mesh,
                Material      = material,
                BoundsCenter  = bounds.center,
                BoundsExtents = bounds.extents
            };
        }

        // Asteroid (Sphere)
        {
            Mesh mesh = GetPrimitiveMesh(PrimitiveType.Sphere);
            var bounds = mesh.bounds;

            entries[2] = new Space4XRenderCatalogDefinition.Entry
            {
                ArchetypeId   = Space4XRenderKeys.Asteroid,
                Mesh          = mesh,
                Material      = material,
                BoundsCenter  = bounds.center,
                BoundsExtents = bounds.extents
            };
        }

        catalog.Entries = entries;

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
