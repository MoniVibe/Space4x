using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using PureDOTS.Editor.MCP;
using System.Collections.Generic;

namespace PureDOTS.Editor.MCP.Tests
{
    /// <summary>
    /// Quick test for describe_vfx_graph parameter extraction
    /// </summary>
    public static class TestDescribeVfxGraph
    {
        [MenuItem("Tools/MCP/Test Describe VFX Graph")]
        public static void Test()
        {
            Debug.Log("=== Testing describe_vfx_graph parameter extraction ===");
            
            var graphPath = "Assets/VFX/BlueSnowOrb.vfx";
            var paramsObj = JObject.FromObject(new
            {
                graph_path = graphPath,
                lod = 2
            });
            
            var result = DescribeVfxGraphTool.HandleCommand(paramsObj);
            
            Debug.Log($"Result type: {result?.GetType()?.FullName}");
            
            // Handle JObject (Response.Success returns JObject)
            if (result is JObject jObj)
            {
                var success = jObj["success"]?.ToObject<bool>() == true;
                Debug.Log($"Success: {success}");
                
                if (success)
                {
                    var data = jObj["data"] as JObject;
                    if (data != null)
                    {
                        var exposedParams = data["exposed_params"] as JArray;
                        if (exposedParams != null)
                        {
                            Debug.Log($"Found {exposedParams.Count} exposed parameters");
                            foreach (var param in exposedParams)
                            {
                                Debug.Log($"  Parameter: {param}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("exposed_params is null or not an array");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("data is null");
                    }
                }
                
                Debug.Log($"Full response: {jObj.ToString(Newtonsoft.Json.Formatting.Indented)}");
            }
            // Handle Dictionary (fallback)
            else if (result is Dictionary<string, object> dict)
            {
                var success = dict.ContainsKey("success") && dict["success"] as bool? == true;
                Debug.Log($"Success: {success}");
                
                if (success && dict.ContainsKey("data"))
                {
                    var data = dict["data"] as Dictionary<string, object>;
                    if (data != null && data.ContainsKey("exposed_params"))
                    {
                        var exposedParams = data["exposed_params"];
                        Debug.Log($"Found {exposedParams} exposed parameters");
                        
                        if (exposedParams is System.Collections.ICollection collection)
                        {
                            Debug.Log($"Parameter count: {collection.Count}");
                            foreach (var param in collection)
                            {
                                Debug.Log($"  Parameter: {param}");
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("No exposed_params in response");
                    }
                }
                
                Debug.Log($"Full response: {Newtonsoft.Json.JsonConvert.SerializeObject(dict, Newtonsoft.Json.Formatting.Indented)}");
            }
            else
            {
                Debug.LogError($"Unexpected result type: {result?.GetType()}");
                Debug.LogError($"Result value: {result}");
            }
        }
    }
}

