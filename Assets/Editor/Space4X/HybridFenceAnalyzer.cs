using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Space4X.Authoring;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// No-hybrid fence analyzer - ensures generated prefabs never introduce forbidden hybrid references into DOTS pipelines.
    /// </summary>
    public static class HybridFenceAnalyzer
    {
        public class HybridViolation
        {
            public string PrefabPath;
            public string ComponentType;
            public string FieldName;
            public string ViolationType; // GameObject, Transform, MonoBehaviour reference
        }

        private static readonly HashSet<System.Type> ForbiddenTypes = new HashSet<System.Type>
        {
            typeof(GameObject),
            typeof(Transform),
            typeof(MonoBehaviour),
            typeof(Component)
        };

        public static List<HybridViolation> AnalyzePrefab(GameObject prefab, string prefabPath)
        {
            var violations = new List<HybridViolation>();

            // Check all authoring components
            var components = prefab.GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                if (component == null) continue;

                // Skip non-authoring components (Unity built-ins)
                if (!component.GetType().Namespace?.StartsWith("Space4X") ?? true) continue;

                var violationsInComponent = AnalyzeComponent(component, prefabPath);
                violations.AddRange(violationsInComponent);
            }

            return violations;
        }

        private static List<HybridViolation> AnalyzeComponent(MonoBehaviour component, string prefabPath)
        {
            var violations = new List<HybridViolation>();
            var componentType = component.GetType();

            // Check all serialized fields
            var fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                // Skip if not serialized
                if (!field.IsPublic && !System.Attribute.IsDefined(field, typeof(SerializeField))) continue;

                var fieldType = field.FieldType;

                // Check for forbidden types
                if (ForbiddenTypes.Contains(fieldType))
                {
                    violations.Add(new HybridViolation
                    {
                        PrefabPath = prefabPath,
                        ComponentType = componentType.Name,
                        FieldName = field.Name,
                        ViolationType = fieldType.Name
                    });
                }

                // Check arrays/lists
                if (fieldType.IsArray)
                {
                    var elementType = fieldType.GetElementType();
                    if (ForbiddenTypes.Contains(elementType))
                    {
                        violations.Add(new HybridViolation
                        {
                            PrefabPath = prefabPath,
                            ComponentType = componentType.Name,
                            FieldName = field.Name,
                            ViolationType = $"{elementType.Name}[]"
                        });
                    }
                }
                else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = fieldType.GetGenericArguments()[0];
                    if (ForbiddenTypes.Contains(elementType))
                    {
                        violations.Add(new HybridViolation
                        {
                            PrefabPath = prefabPath,
                            ComponentType = componentType.Name,
                            FieldName = field.Name,
                            ViolationType = $"List<{elementType.Name}>"
                        });
                    }
                }
            }

            return violations;
        }

        public static List<HybridViolation> AnalyzeAllPrefabs(string prefabBasePath = "Assets/Prefabs/Space4X")
        {
            var violations = new List<HybridViolation>();

            var prefabDirs = new[] { "Hulls", "CapitalShips", "Carriers", "Stations", "Modules", "Resources", "Products", "Aggregates", "FX", "Individuals" };
            
            foreach (var dir in prefabDirs)
            {
                var fullDir = $"{prefabBasePath}/{dir}";
                if (!System.IO.Directory.Exists(fullDir)) continue;

                var prefabs = System.IO.Directory.GetFiles(fullDir, "*.prefab", System.IO.SearchOption.AllDirectories);
                foreach (var prefabPath in prefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null) continue;

                    var prefabViolations = AnalyzePrefab(prefab, prefabPath);
                    violations.AddRange(prefabViolations);
                }
            }

            return violations;
        }
    }
}

