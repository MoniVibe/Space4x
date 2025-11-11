using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Test script to verify new VFX Graph tools are working.
    /// Run this from the Unity menu: Tools > MCP > Test New VFX Tools
    /// </summary>
    public static class TestNewVfxTools
    {
        [MenuItem("Tools/MCP/Test New VFX Tools")]
        public static void Test()
        {
            Debug.Log("=== Testing New VFX Graph Tools ===");

            var graphPath = "Assets/Samples/Visual Effect Graph/17.2.0/Learning Templates/VFX/BasicTexIndex.vfx";

            // Test 1: List context blocks
            Debug.Log("Test 1: Listing context blocks...");
            try
            {
                var @params = new JObject
                {
                    ["graph_path"] = graphPath,
                    ["context_id"] = 69626 // VFXBasicInitialize
                };
                var result = ListContextBlocksTool.HandleCommand(@params);
                Debug.Log($"ListContextBlocks result: {result}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Test 1 failed: {ex.Message}\n{ex.StackTrace}");
            }

            // Test 2: Describe node ports
            Debug.Log("Test 2: Describing node ports...");
            try
            {
                var @params = new JObject
                {
                    ["graph_path"] = graphPath,
                    ["node_id"] = 69714 // Operator.Modulo
                };
                var result = DescribeNodePortsTool.HandleCommand(@params);
                Debug.Log($"DescribeNodePorts result: {result}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Test 2 failed: {ex.Message}\n{ex.StackTrace}");
            }

            // Test 3: Describe node settings
            Debug.Log("Test 3: Describing node settings...");
            try
            {
                var @params = new JObject
                {
                    ["graph_path"] = graphPath,
                    ["node_id"] = 69714 // Operator.Modulo
                };
                var result = DescribeNodeSettingsTool.HandleCommand(@params);
                Debug.Log($"DescribeNodeSettings result: {result}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Test 3 failed: {ex.Message}\n{ex.StackTrace}");
            }

            // Test 4: List exposed parameters
            Debug.Log("Test 4: Listing exposed parameters...");
            try
            {
                var @params = new JObject
                {
                    ["graph_path"] = graphPath
                };
                var result = ListExposedParametersTool.HandleCommand(@params);
                Debug.Log($"ListExposedParameters result: {result}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Test 4 failed: {ex.Message}\n{ex.StackTrace}");
            }

            // Test 5: Set slot value
            Debug.Log("Test 5: Setting slot value...");
            try
            {
                var @params = new JObject
                {
                    ["graph_path"] = graphPath,
                    ["node_id"] = 69714, // Operator.Modulo
                    ["slot_name"] = "a",
                    ["slot_value"] = 10.0f
                };
                var result = SetSlotValueTool.HandleCommand(@params);
                Debug.Log($"SetSlotValue result: {result}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Test 5 failed: {ex.Message}\n{ex.StackTrace}");
            }

            Debug.Log("=== New VFX Graph Tools Test Complete ===");
        }
    }
}

