using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Comprehensive demonstration of all new VFX Graph tools.
    /// Run from: Tools > MCP > Demo New VFX Tools
    /// </summary>
    public static class DemoNewVfxTools
    {
        [MenuItem("Tools/MCP/Demo New VFX Tools")]
        public static void Demo()
        {
            var graphPath = "Assets/Samples/Visual Effect Graph/17.2.0/Learning Templates/VFX/BasicTexIndex.vfx";
            
            Debug.Log("=== DEMONSTRATING NEW VFX GRAPH TOOLS ===\n");

            // 1. List Context Blocks
            Debug.Log("1. LISTING CONTEXT BLOCKS");
            Debug.Log("   Testing: ListContextBlocksTool");
            try
            {
                var params1 = JObject.FromObject(new
                {
                    graph_path = graphPath,
                    context_id = 69626 // VFXBasicInitialize
                });
                var result1 = ListContextBlocksTool.HandleCommand(params1);
                Debug.Log($"   ✓ Success: {result1}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"   ✗ Failed: {ex.Message}");
            }

            // 2. Describe Node Ports
            Debug.Log("\n2. DESCRIBING NODE PORTS");
            Debug.Log("   Testing: DescribeNodePortsTool");
            try
            {
                var params2 = JObject.FromObject(new
                {
                    graph_path = graphPath,
                    node_id = 69714 // Operator.Modulo
                });
                var result2 = DescribeNodePortsTool.HandleCommand(params2);
                Debug.Log($"   ✓ Success: {result2}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"   ✗ Failed: {ex.Message}");
            }

            // 3. Describe Node Settings
            Debug.Log("\n3. DESCRIBING NODE SETTINGS");
            Debug.Log("   Testing: DescribeNodeSettingsTool");
            try
            {
                var params3 = JObject.FromObject(new
                {
                    graph_path = graphPath,
                    node_id = 69714 // Operator.Modulo
                });
                var result3 = DescribeNodeSettingsTool.HandleCommand(params3);
                Debug.Log($"   ✓ Success: {result3}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"   ✗ Failed: {ex.Message}");
            }

            // 4. List Exposed Parameters
            Debug.Log("\n4. LISTING EXPOSED PARAMETERS");
            Debug.Log("   Testing: ListExposedParametersTool");
            try
            {
                var params4 = JObject.FromObject(new
                {
                    graph_path = graphPath
                });
                var result4 = ListExposedParametersTool.HandleCommand(params4);
                Debug.Log($"   ✓ Success: {result4}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"   ✗ Failed: {ex.Message}");
            }

            // 5. Set Slot Value
            Debug.Log("\n5. SETTING SLOT VALUE");
            Debug.Log("   Testing: SetSlotValueTool");
            try
            {
                var params5 = JObject.FromObject(new
                {
                    graph_path = graphPath,
                    node_id = 69714, // Operator.Modulo
                    slot_name = "a",
                    slot_value = 42.0f
                });
                var result5 = SetSlotValueTool.HandleCommand(params5);
                Debug.Log($"   ✓ Success: {result5}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"   ✗ Failed: {ex.Message}");
            }

            // 6. List Graph Variants (for discovery)
            Debug.Log("\n6. LISTING GRAPH VARIANTS");
            Debug.Log("   Testing: ListGraphVariantsTool");
            try
            {
                var params6 = JObject.FromObject(new
                {
                    category = "Operator",
                    limit = 10
                });
                var result6 = ListGraphVariantsTool.HandleCommand(params6);
                Debug.Log($"   ✓ Success: Found variants");
            }
            catch (Exception ex)
            {
                Debug.LogError($"   ✗ Failed: {ex.Message}");
            }

            Debug.Log("\n=== DEMONSTRATION COMPLETE ===");
            Debug.Log("All new VFX Graph tools have been tested!");
            Debug.Log("Check the console above for detailed results.");
        }
    }
}

