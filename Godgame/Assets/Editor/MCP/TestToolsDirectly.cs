using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Direct test of new VFX tools by calling handlers.
    /// </summary>
    public static class TestToolsDirectly
    {
        [MenuItem("Tools/MCP/Test New Tools Directly")]
        public static void Test()
        {
            var graphPath = "Assets/Samples/Visual Effect Graph/17.2.0/Learning Templates/VFX/BasicTexIndex.vfx";
            
            Debug.Log("=== Testing New VFX Tools Directly ===");

            // Test DescribeNodePorts
            try
            {
                Debug.Log("Testing DescribeNodePorts...");
                var params1 = JObject.FromObject(new
                {
                    graph_path = graphPath,
                    node_id = 69714 // Operator.Modulo from earlier test
                });
                var result1 = DescribeNodePortsTool.HandleCommand(params1);
                Debug.Log($"DescribeNodePorts: {result1}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"DescribeNodePorts failed: {ex}");
            }

            // Test ListContextBlocks
            try
            {
                Debug.Log("Testing ListContextBlocks...");
                var params2 = JObject.FromObject(new
                {
                    graph_path = graphPath,
                    context_id = 69626 // VFXBasicInitialize
                });
                var result2 = ListContextBlocksTool.HandleCommand(params2);
                Debug.Log($"ListContextBlocks: {result2}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ListContextBlocks failed: {ex}");
            }

            // Test ListExposedParameters
            try
            {
                Debug.Log("Testing ListExposedParameters...");
                var params3 = JObject.FromObject(new
                {
                    graph_path = graphPath
                });
                var result3 = ListExposedParametersTool.HandleCommand(params3);
                Debug.Log($"ListExposedParameters: {result3}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ListExposedParameters failed: {ex}");
            }

            Debug.Log("=== Test Complete ===");
        }
    }
}

