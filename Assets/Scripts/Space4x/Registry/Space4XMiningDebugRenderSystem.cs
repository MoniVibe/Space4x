using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// Renders simple primitives for carriers, mining vessels, and asteroids so the mining loop is visible in hybrid scenes.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct Space4XMiningDebugRenderSystem : ISystem
    {
        private bool _loggedCarrier;
        private bool _loggedVessel;
        private bool _loggedAsteroid;
        private bool _loggedSystemRunning;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XMiningVisualConfig>();
            _loggedCarrier = false;
            _loggedVessel = false;
            _loggedAsteroid = false;
            _loggedSystemRunning = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode == RewindMode.Playback)
            {
                return;
            }

            // Log system execution once to confirm it's running
            if (!_loggedSystemRunning)
            {
                var carrierQuery = SystemAPI.QueryBuilder().WithAll<Carrier, LocalTransform>().Build();
                var vesselQuery = SystemAPI.QueryBuilder().WithAll<MiningVessel, LocalTransform>().Build();
                var asteroidQuery = SystemAPI.QueryBuilder().WithAll<Asteroid, LocalTransform>().Build();
                
                var carrierCount = carrierQuery.CalculateEntityCount();
                var vesselCount = vesselQuery.CalculateEntityCount();
                var asteroidCount = asteroidQuery.CalculateEntityCount();
                
                Debug.Log($"[Space4XMiningDebugRenderSystem] System is running. Entities: Carriers={carrierCount}, Vessels={vesselCount}, Asteroids={asteroidCount}");
                
                // Also log positions of first few entities to verify they exist
                if (carrierCount > 0)
                {
                    using var carriers = carrierQuery.ToEntityArray(Allocator.Temp);
                    for (int i = 0; i < math.min(3, carriers.Length); i++)
                    {
                        var transform = SystemAPI.GetComponent<LocalTransform>(carriers[i]);
                        Debug.Log($"[Space4XMiningDebugRenderSystem] Carrier {i}: Position={transform.Position}");
                    }
                }
                
                _loggedSystemRunning = true;
            }

            var config = SystemAPI.GetSingleton<Space4XMiningVisualConfig>();

            var carrierMesh = MiningDebugRenderResources.GetMesh(config.CarrierPrimitive);
            var carrierMaterial = MiningDebugRenderResources.GetMaterial(config.CarrierColor);
            var vesselMesh = MiningDebugRenderResources.GetMesh(config.MiningVesselPrimitive);
            var vesselMaterial = MiningDebugRenderResources.GetMaterial(config.MiningVesselColor);
            var asteroidMesh = MiningDebugRenderResources.GetMesh(config.AsteroidPrimitive);
            var asteroidMaterial = MiningDebugRenderResources.GetMaterial(config.AsteroidColor);
            
            // Log once per entity type if materials are null or have wrong shader (helps diagnose purple materials)
            if (carrierMaterial == null && !_loggedCarrier)
            {
                Debug.LogError("[Space4XMiningDebugRenderSystem] Carrier material is NULL! Carriers will not render.");
                _loggedCarrier = true;
            }
            else if (carrierMaterial != null && carrierMaterial.shader != null && carrierMaterial.shader.name == "Hidden/InternalErrorShader" && !_loggedCarrier)
            {
                Debug.LogError($"[Space4XMiningDebugRenderSystem] Carrier material uses error shader! Shader: {carrierMaterial.shader.name}, Color: {carrierMaterial.color}");
                _loggedCarrier = true;
            }
            
            if (vesselMaterial == null && !_loggedVessel)
            {
                Debug.LogError("[Space4XMiningDebugRenderSystem] Vessel material is NULL! Vessels will not render.");
                _loggedVessel = true;
            }
            else if (vesselMaterial != null && vesselMaterial.shader != null && vesselMaterial.shader.name == "Hidden/InternalErrorShader" && !_loggedVessel)
            {
                Debug.LogError($"[Space4XMiningDebugRenderSystem] Vessel material uses error shader! Shader: {vesselMaterial.shader.name}, Color: {vesselMaterial.color}");
                _loggedVessel = true;
            }
            
            if (asteroidMaterial == null && !_loggedAsteroid)
            {
                Debug.LogError("[Space4XMiningDebugRenderSystem] Asteroid material is NULL! Asteroids will not render.");
                _loggedAsteroid = true;
            }
            else if (asteroidMaterial != null && asteroidMaterial.shader != null && asteroidMaterial.shader.name == "Hidden/InternalErrorShader" && !_loggedAsteroid)
            {
                Debug.LogError($"[Space4XMiningDebugRenderSystem] Asteroid material uses error shader! Shader: {asteroidMaterial.shader.name}, Color: {asteroidMaterial.color}");
                _loggedAsteroid = true;
            }

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Carrier>())
            {
                MiningDebugRenderResources.DrawMesh(carrierMesh, carrierMaterial, transform.ValueRO, config.CarrierScale);
            }

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<MiningVessel>())
            {
                MiningDebugRenderResources.DrawMesh(vesselMesh, vesselMaterial, transform.ValueRO, config.MiningVesselScale);
            }

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Asteroid>())
            {
                MiningDebugRenderResources.DrawMesh(asteroidMesh, asteroidMaterial, transform.ValueRO, config.AsteroidScale);
            }
        }
    }

    internal static class MiningDebugRenderResources
    {
        private static readonly Dictionary<Space4XMiningPrimitive, Mesh> MeshCache = new();
        private static readonly Dictionary<int, Material> MaterialCache = new();

        public static Mesh GetMesh(Space4XMiningPrimitive primitive)
        {
            if (MeshCache.TryGetValue(primitive, out var mesh) && mesh != null)
            {
                return mesh;
            }

            var unityPrimitive = primitive switch
            {
                Space4XMiningPrimitive.Sphere => PrimitiveType.Sphere,
                Space4XMiningPrimitive.Capsule => PrimitiveType.Capsule,
                Space4XMiningPrimitive.Cylinder => PrimitiveType.Cylinder,
                _ => PrimitiveType.Cube
            };

            var temp = GameObject.CreatePrimitive(unityPrimitive);
            var filter = temp.GetComponent<MeshFilter>();
            mesh = filter != null ? filter.sharedMesh : null;

            if (Application.isPlaying)
            {
                Object.Destroy(temp);
            }
            else
            {
                Object.DestroyImmediate(temp);
            }

            if (mesh != null)
            {
                MeshCache[primitive] = mesh;
            }

            return mesh;
        }

        public static Material GetMaterial(float4 rgba)
        {
            var color = new Color(math.saturate(rgba.x), math.saturate(rgba.y), math.saturate(rgba.z), math.saturate(rgba.w));
            var key = color.GetHashCode();

            if (MaterialCache.TryGetValue(key, out var material) && material != null)
            {
                return material;
            }

            // Try shaders in order of preference - Built-in shaders work without SRP
            Shader shader = null;
            string[] shaderNames = new[]
            {
                "Unlit/Color",  // Best fallback - always works and supports color (built-in)
                "Unlit/Texture", // Built-in unlit shader
                "Standard",      // Built-in standard shader
                "Legacy Shaders/Diffuse", // Built-in legacy shader
                "Sprites/Default", // Built-in sprite shader
                "Universal Render Pipeline/Lit", // URP (requires SRP)
                "Universal Render Pipeline/Simple Lit" // URP (requires SRP)
            };

            foreach (var shaderName in shaderNames)
            {
                shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    break;
                }
            }

            // If no shader found at all, something is very wrong - but we'll try anyway
            if (shader == null)
            {
                Debug.LogError("[Space4XMiningDebugRenderSystem] No valid shader found! Entities will appear purple. Check Graphics Settings.");
                Debug.LogError("[Space4XMiningDebugRenderSystem] Available shaders: Unlit/Color, Standard, Sprites/Default should be available.");
                // Last resort: try to create a material with the error shader
                shader = Shader.Find("Hidden/InternalErrorShader");
                if (shader == null)
                {
                    // Try one more time with Sprites/Default
                    shader = Shader.Find("Sprites/Default");
                    if (shader == null)
                    {
                        Debug.LogError("[Space4XMiningDebugRenderSystem] CRITICAL: No shaders available at all! Check Unity installation.");
                        MaterialCache[key] = null; // Cache null to avoid repeated errors
                        return null;
                    }
                }
            }

            material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            material.enableInstancing = true;

            // Set color properties (try multiple property names for compatibility)
            // Unlit/Color uses "_Color", URP uses "_BaseColor", Standard uses "_Color"
            bool colorSet = false;
            
            // Always set material.color first (works for most shaders)
            material.color = color;
            
            // Then try shader-specific properties
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
                colorSet = true;
            }
            
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
                colorSet = true;
            }
            
            if (material.HasProperty("_MainColor"))
            {
                material.SetColor("_MainColor", color);
                colorSet = true;
            }
            
            if (material.HasProperty("_TintColor"))
            {
                material.SetColor("_TintColor", color);
                colorSet = true;
            }
            
            // Log which shader was used for debugging (only first time per color to avoid spam)
            if (MaterialCache.Count == 0 || UnityEngine.Time.frameCount <= 1)
            {
                Debug.Log($"[Space4XMiningDebugRenderSystem] Created material with shader '{shader.name}', color={color}, colorSet={colorSet}, HasShader={material.shader != null}, ShaderName={material.shader?.name}");
            }

            MaterialCache[key] = material;
            return material;
        }

        // Use a static class to track logging state across DrawMesh calls
        private static class DrawMeshLogger
        {
            public static bool loggedNullShader;
            public static bool loggedErrorShader;
            public static bool loggedNoCamera;
            public static int drawCount;
        }

        public static void DrawMesh(Mesh mesh, Material material, in LocalTransform transform, float uniformScale)
        {
            if (mesh == null || material == null)
            {
                return;
            }

            var scale = math.max(0.0001f, transform.Scale) * math.max(0.0001f, uniformScale);
            var matrix = Matrix4x4.TRS(
                (Vector3)transform.Position,
                new Quaternion(transform.Rotation.value.x, transform.Rotation.value.y, transform.Rotation.value.z, transform.Rotation.value.w),
                new Vector3(scale, scale, scale));

            // Ensure material has a valid shader before drawing
            if (material.shader == null)
            {
                if (!DrawMeshLogger.loggedNullShader)
                {
                    Debug.LogError($"[Space4XMiningDebugRenderSystem] Material has NULL shader! Cannot render mesh. Material may not have been created correctly.");
                    DrawMeshLogger.loggedNullShader = true;
                }
                return;
            }
            
            if (material.shader.name == "Hidden/InternalErrorShader")
            {
                if (!DrawMeshLogger.loggedErrorShader)
                {
                    Debug.LogError($"[Space4XMiningDebugRenderSystem] Material uses error shader '{material.shader.name}'! This causes purple rendering. Check Graphics Settings.");
                    DrawMeshLogger.loggedErrorShader = true;
                }
                return;
            }

            // Graphics.DrawMesh requires the material to have a valid shader and be enabled
            // Layer 0 is the default layer, which should be visible to all cameras
            // IMPORTANT: Graphics.DrawMesh must be called with a camera parameter for proper rendering
            var camera = Camera.main;
            if (camera == null)
            {
                camera = Camera.current;
            }
            
            if (camera == null)
            {
                // Try to find any active camera
                camera = Object.FindFirstObjectByType<Camera>();
            }

            if (camera != null)
            {
                // Use Graphics.DrawMesh with explicit camera and layer
                Graphics.DrawMesh(mesh, matrix, material, 0, camera, 0, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
                
                // Also log occasionally to confirm rendering is happening (use static class for cross-call tracking)
                DrawMeshLogger.drawCount++;
#if UNITY_EDITOR && SPACE4X_DEBUG_MINING
                if (DrawMeshLogger.drawCount % 100 == 0)
                {
                    Debug.Log($"[Space4XMiningDebugRenderSystem] DrawMesh called {DrawMeshLogger.drawCount} times. Mesh={mesh.name}, Material={material.shader.name}, Camera={camera.name}");
                }
#endif
            }
            else
            {
                if (!DrawMeshLogger.loggedNoCamera)
                {
                    Debug.LogWarning("[Space4XMiningDebugRenderSystem] No Camera found! Graphics.DrawMesh cannot render. Check scene has a Camera.");
                    DrawMeshLogger.loggedNoCamera = true;
                }
            }
        }
    }
}












