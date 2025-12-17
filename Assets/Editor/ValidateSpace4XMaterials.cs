using System.Linq;
using UnityEngine;
using UnityEditor;
using Space4X.Presentation;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Validates all materials and meshes used by Space4X mining debug rendering.
    /// Helps diagnose purple material issues.
    /// </summary>
    public static class ValidateSpace4XMaterials
    {
        [MenuItem("Tools/Space4X/Validate Materials and Meshes")]
        public static void ValidateAll()
        {
            UnityDebug.Log("=== Space4X Materials and Meshes Validation ===\n");

            // Find all Space4XMiningDemoAuthoring components (the authoring version)
            var authorings = Resources.FindObjectsOfTypeAll<MonoBehaviour>().OfType<Space4X.Registry.Space4XMiningDemoAuthoring>().ToArray();
            
            if (authorings.Length == 0)
            {
                UnityDebug.LogWarning("No Space4XMiningDemoAuthoring found in open scenes!");
                UnityDebug.LogWarning("The config might be baked into a subscene. Continuing with general validation...\n");
            }
            else
            {
                UnityDebug.Log($"Found {authorings.Length} Space4XMiningDemoAuthoring component(s):\n");
                foreach (var authoring in authorings)
                {
                    ValidateAuthoring(authoring);
                }
            }

            // Validate shader availability
            ValidateShaderAvailability();

            // Try to create test materials
            TestMaterialCreation();

            UnityDebug.Log("\n=== Validation Complete ===");
        }

        private static void ValidateAuthoring(Space4X.Registry.Space4XMiningDemoAuthoring authoring)
        {
            UnityDebug.Log($"\n--- Validating Space4XMiningDemoAuthoring ---");
            UnityDebug.Log($"GameObject: {authoring.gameObject.name}");
            UnityDebug.Log($"Scene: {authoring.gameObject.scene.name}");
            
            // Get the visual settings from the authoring component
            var visuals = authoring.Visuals;
            
            // Convert Unity Colors to float4 (same as baker does)
            float4 CarrierColor = ToFloat4(visuals.CarrierColor);
            float4 VesselColor = ToFloat4(visuals.MiningVesselColor);
            float4 AsteroidColor = ToFloat4(visuals.AsteroidColor);

            UnityDebug.Log($"\nColors:");
            UnityDebug.Log($"  Carrier: R={CarrierColor.x:F2}, G={CarrierColor.y:F2}, B={CarrierColor.z:F2}, A={CarrierColor.w:F2}");
            UnityDebug.Log($"  Vessel: R={VesselColor.x:F2}, G={VesselColor.y:F2}, B={VesselColor.z:F2}, A={VesselColor.w:F2}");
            UnityDebug.Log($"  Asteroid: R={AsteroidColor.x:F2}, G={AsteroidColor.y:F2}, B={AsteroidColor.z:F2}, A={AsteroidColor.w:F2}");

            UnityDebug.Log($"\nPrimitives:");
            UnityDebug.Log($"  Carrier: {visuals.CarrierPrimitive}");
            UnityDebug.Log($"  Vessel: {visuals.MiningVesselPrimitive}");
            UnityDebug.Log($"  Asteroid: {visuals.AsteroidPrimitive}");

            UnityDebug.Log($"\nScales:");
            UnityDebug.Log($"  Carrier: {visuals.CarrierScale}");
            UnityDebug.Log($"  Vessel: {visuals.MiningVesselScale}");
            UnityDebug.Log($"  Asteroid: {visuals.AsteroidScale}");

            // Test material creation
            UnityDebug.Log($"\nMaterial Creation Test:");
            TestMaterialForColor("Carrier", CarrierColor);
            TestMaterialForColor("Vessel", VesselColor);
            TestMaterialForColor("Asteroid", AsteroidColor);

            // Test mesh creation
            UnityDebug.Log($"\nMesh Creation Test:");
            TestMeshForPrimitive("Carrier", visuals.CarrierPrimitive);
            TestMeshForPrimitive("Vessel", visuals.MiningVesselPrimitive);
            TestMeshForPrimitive("Asteroid", visuals.AsteroidPrimitive);
        }

        private static float4 ToFloat4(Color color)
        {
            return new float4(color.r, color.g, color.b, color.a);
        }

        private static void TestMaterialForColor(string name, Unity.Mathematics.float4 color)
        {
            var unityColor = new Color(color.x, color.y, color.z, color.w);
            
            // Try to create material using the same logic as MiningDebugRenderResources
            Material material = null;
            Shader shader = null;

            string[] shaderNames = new[]
            {
                "Unlit/Color",
                "Unlit/Texture",
                "Standard",
                "Legacy Shaders/Diffuse",
                "Sprites/Default",
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit"
            };

            foreach (var shaderName in shaderNames)
            {
                shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    UnityDebug.Log($"  {name}: Found shader '{shaderName}'");
                    break;
                }
            }

            if (shader == null)
            {
                UnityDebug.LogError($"  {name}: NO VALID SHADER FOUND! This is why materials are purple!");
                UnityDebug.LogError($"  {name}: Checked shaders: {string.Join(", ", shaderNames)}");
                return;
            }

            if (shader.name == "Hidden/InternalErrorShader")
            {
                UnityDebug.LogError($"  {name}: Shader is InternalErrorShader - this causes purple rendering!");
                return;
            }

            try
            {
                material = new Material(shader);
                material.color = unityColor;

                // Set shader-specific properties
                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", unityColor);
                }
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", unityColor);
                }
                if (material.HasProperty("_MainColor"))
                {
                    material.SetColor("_MainColor", unityColor);
                }
                if (material.HasProperty("_TintColor"))
                {
                    material.SetColor("_TintColor", unityColor);
                }

                UnityDebug.Log($"  {name}: Material created successfully with shader '{shader.name}'");
                UnityDebug.Log($"  {name}: Material color: {material.color}");
                UnityDebug.Log($"  {name}: Material shader: {material.shader.name}");
                UnityDebug.Log($"  {name}: Has _Color property: {material.HasProperty("_Color")}");
                UnityDebug.Log($"  {name}: Has _BaseColor property: {material.HasProperty("_BaseColor")}");

                // Clean up
                Object.DestroyImmediate(material);
            }
            catch (System.Exception ex)
            {
                UnityDebug.LogError($"  {name}: Failed to create material: {ex.Message}");
                UnityDebug.LogError($"  {name}: Stack trace: {ex.StackTrace}");
            }
        }

        private static void TestMeshForPrimitive(string name, Space4XMiningPrimitive primitive)
        {
            var unityPrimitive = primitive switch
            {
                Space4XMiningPrimitive.Sphere => PrimitiveType.Sphere,
                Space4XMiningPrimitive.Capsule => PrimitiveType.Capsule,
                Space4XMiningPrimitive.Cylinder => PrimitiveType.Cylinder,
                _ => PrimitiveType.Cube
            };

            try
            {
                var temp = GameObject.CreatePrimitive(unityPrimitive);
                var filter = temp.GetComponent<MeshFilter>();
                
                if (filter != null && filter.sharedMesh != null)
                {
                    var mesh = filter.sharedMesh;
                    UnityDebug.Log($"  {name}: Mesh '{primitive}' -> '{unityPrimitive}' created successfully");
                    UnityDebug.Log($"  {name}: Mesh vertices: {mesh.vertexCount}, triangles: {mesh.triangles.Length / 3}");
                }
                else
                {
                    UnityDebug.LogError($"  {name}: Failed to get mesh from primitive '{unityPrimitive}'");
                }

                Object.DestroyImmediate(temp);
            }
            catch (System.Exception ex)
            {
                UnityDebug.LogError($"  {name}: Failed to create mesh: {ex.Message}");
            }
        }

        private static void ValidateShaderAvailability()
        {
            UnityDebug.Log("\n--- Shader Availability Check ---");

            string[] shaderNames = new[]
            {
                "Unlit/Color",
                "Unlit/Texture",
                "Standard",
                "Legacy Shaders/Diffuse",
                "Sprites/Default",
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit"
            };

            foreach (var shaderName in shaderNames)
            {
                var shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    UnityDebug.Log($"✓ '{shaderName}' - Available (Name: {shader.name})");
                }
                else
                {
                    UnityDebug.LogWarning($"✗ '{shaderName}' - NOT FOUND");
                }
            }

            // Check Graphics Settings
            UnityDebug.Log("\nGraphics Settings Check:");
            var renderPipelineAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            if (renderPipelineAsset == null)
            {
                UnityDebug.LogWarning("No Scriptable Render Pipeline (SRP) configured - using Built-in Render Pipeline");
                UnityDebug.LogWarning("This will cause URP shaders to fail. Built-in shaders should still work.");
            }
            else
            {
                UnityDebug.Log($"Render Pipeline: {renderPipelineAsset.GetType().Name}");
            }
        }

        private static void TestMaterialCreation()
        {
            UnityDebug.Log("\n--- Material Creation Test (Runtime Simulation) ---");

            var testColors = new[]
            {
                ("Red", new Unity.Mathematics.float4(1, 0, 0, 1)),
                ("Green", new Unity.Mathematics.float4(0, 1, 0, 1)),
                ("Blue", new Unity.Mathematics.float4(0, 0, 1, 1))
            };

            foreach (var (name, color) in testColors)
            {
                UnityDebug.Log($"\nTesting {name} material:");
                
                // Use the exact same logic as MiningDebugRenderResources.GetMaterial
                var unityColor = new Color(color.x, color.y, color.z, color.w);
                var key = unityColor.GetHashCode();

                Shader shader = null;
                string[] shaderNames = new[]
                {
                    "Unlit/Color",
                    "Unlit/Texture",
                    "Standard",
                    "Legacy Shaders/Diffuse",
                    "Sprites/Default",
                    "Universal Render Pipeline/Lit",
                    "Universal Render Pipeline/Simple Lit"
                };

                foreach (var shaderName in shaderNames)
                {
                    shader = Shader.Find(shaderName);
                    if (shader != null)
                    {
                        UnityDebug.Log($"  Shader found: '{shaderName}'");
                        break;
                    }
                }

                if (shader == null)
                {
                    UnityDebug.LogError($"  ERROR: No shader found! All shaders checked failed.");
                    continue;
                }

                if (shader.name == "Hidden/InternalErrorShader")
                {
                    UnityDebug.LogError($"  ERROR: Shader is InternalErrorShader! This means shader lookup failed.");
                    continue;
                }

                var material = new Material(shader);
                material.color = unityColor;

                bool colorSet = false;
                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", unityColor);
                    colorSet = true;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", unityColor);
                    colorSet = true;
                }

                if (material.HasProperty("_MainColor"))
                {
                    material.SetColor("_MainColor", unityColor);
                    colorSet = true;
                }

                if (material.HasProperty("_TintColor"))
                {
                    material.SetColor("_TintColor", unityColor);
                    colorSet = true;
                }

                UnityDebug.Log($"  Material created: Shader={material.shader.name}, Color={material.color}, ColorSet={colorSet}");
                UnityDebug.Log($"  Material shader name match: {material.shader.name == shader.name}");

                // Verify the material will render correctly
                if (material.shader.name == "Hidden/InternalErrorShader")
                {
                    UnityDebug.LogError($"  WARNING: Final material has error shader! This will render purple!");
                }

                Object.DestroyImmediate(material);
            }
        }
    }
}

