using UnityEngine;
using UnityEditor;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Test script to verify VFX Graph tools are working.
    /// Run this from the Unity menu: Tools > MCP > Test VFX Graph Tools
    /// </summary>
    public static class TestVfxGraphTools
    {
        [MenuItem("Tools/MCP/Test VFX Graph Tools")]
        public static void Test()
        {
            Debug.Log("=== Testing VFX Graph Tools ===");

            // Test 1: List variants
            Debug.Log("Test 1: Listing VFX variants...");
            try
            {
                var variants = VfxGraphReflectionHelpers.GetLibraryDescriptors("GetOperators");
                Debug.Log($"Found {variants.Count()} operator descriptors");
                
                var contexts = VfxGraphReflectionHelpers.GetLibraryDescriptors("GetContexts");
                Debug.Log($"Found {contexts.Count()} context descriptors");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Test 1 failed: {ex.Message}");
            }

            // Test 2: Try to load a VFX graph
            Debug.Log("Test 2: Loading VFX graph...");
            try
            {
                var graphPath = "Assets/Samples/Visual Effect Graph/17.2.0/Learning Templates/VFX/BasicTexIndex.vfx";
                if (VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    Debug.Log($"Successfully loaded graph: {graphPath}");
                    
                    if (VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out error))
                    {
                        Debug.Log("Successfully created view controller");
                        
                        var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
                        var nodeCount = VfxGraphReflectionHelpers.Enumerate(nodesEnumerable).Cast<object>().Count();
                        Debug.Log($"Graph has {nodeCount} nodes");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to create controller: {error}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to load graph: {error}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Test 2 failed: {ex.Message}");
            }

            Debug.Log("=== VFX Graph Tools Test Complete ===");
        }
    }
}

