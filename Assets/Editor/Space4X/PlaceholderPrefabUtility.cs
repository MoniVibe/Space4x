using Space4X.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Space4X.Editor
{
    /// <summary>
    /// Utility for creating placeholder visuals (primitive meshes) for prefabs.
    /// Inspired by Godgame's PlaceholderPrefabUtility pattern.
    /// </summary>
    public static class PlaceholderPrefabUtility
    {
        /// <summary>
        /// Adds a placeholder visual as a child GameObject with appropriate primitive mesh.
        /// </summary>
        public static void AddPlaceholderVisual(GameObject parent, PrefabType prefabType, Vector3? scale = null)
        {
            if (parent == null) return;

            // Remove existing visual if present
            var existingVisual = parent.transform.Find("Visual");
            if (existingVisual != null)
            {
                Object.DestroyImmediate(existingVisual.gameObject);
            }

            PrimitiveType primitive;
            Vector3 visualScale;

            switch (prefabType)
            {
                case PrefabType.Hull:
                    primitive = PrimitiveType.Capsule;
                    visualScale = scale ?? new Vector3(1f, 1.5f, 1f);
                    break;
                case PrefabType.Module:
                    primitive = PrimitiveType.Cube;
                    visualScale = scale ?? new Vector3(0.5f, 0.5f, 0.5f);
                    break;
                case PrefabType.Station:
                    primitive = PrimitiveType.Cylinder;
                    visualScale = scale ?? new Vector3(1.5f, 2f, 1.5f);
                    break;
                case PrefabType.FX:
                case PrefabType.Effect:
                    primitive = PrimitiveType.Quad;
                    visualScale = scale ?? Vector3.one;
                    break;
                case PrefabType.CapitalShip:
                    primitive = PrimitiveType.Capsule;
                    visualScale = scale ?? new Vector3(2f, 3f, 2f);
                    break;
                case PrefabType.Carrier:
                    primitive = PrimitiveType.Cube;
                    visualScale = scale ?? new Vector3(2.5f, 2f, 2.5f);
                    break;
                case PrefabType.Resource:
                case PrefabType.Product:
                    primitive = PrimitiveType.Sphere;
                    visualScale = scale ?? new Vector3(0.3f, 0.3f, 0.3f);
                    break;
                case PrefabType.Aggregate:
                    primitive = PrimitiveType.Quad;
                    visualScale = scale ?? new Vector3(0.5f, 0.5f, 0.5f);
                    break;
                default:
                    primitive = PrimitiveType.Sphere;
                    visualScale = scale ?? Vector3.one;
                    break;
            }

            var visual = GameObject.CreatePrimitive(primitive);
            visual.name = "Visual";
            visual.transform.SetParent(parent.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = visualScale;

            // Remove collider (not needed for ECS presentation)
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            // Set up mesh renderer with default material
            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetDefaultMaterial();
            }
        }

        private static Material GetDefaultMaterial()
        {
            // Try to find URP Lit material, fallback to default
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            return shader != null ? new Material(shader) : new Material(Shader.Find("Diffuse"));
        }
    }

    public enum PrefabType
    {
        Hull,
        Module,
        Station,
        FX,
        CapitalShip,
        Carrier,
        Resource,
        Product,
        Aggregate,
        Effect
    }
}

