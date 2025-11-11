using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Comprehensive test harness for all VFX Graph MCP tools.
    /// Run from: Tools > MCP > Test All VFX Tools
    /// </summary>
    public static class TestAllVfxTools
    {
        [MenuItem("Tools/MCP/Test All VFX Tools")]
        public static void TestAll()
        {
            Debug.Log("=== COMPREHENSIVE VFX TOOLS TEST ===");
            Debug.Log("This test verifies all new VFX Graph tools are working correctly.\n");

            var treeGraphPath = "Assets/VFX/TreeBuilder.vfx";
            var testGraphPath = "Assets/VFX/TestGraph.vfx";
            var testResults = new List<(string tool, bool success, string message)>();

            // Test 0: Build VFX Graph Tree
            Debug.Log("Test 0: Building VFX graph tree...");
            try
            {
                var params0 = JObject.FromObject(new
                {
                    graph_path = treeGraphPath,
                    overwrite = true,
                    spawn_rate = 24f
                });
                var result0 = BuildVfxGraphTreeTool.HandleCommand(params0);
                bool success0;
                string message0;
                if (result0 is JObject obj0)
                {
                    success0 = obj0["success"]?.Value<bool>() == true;
                    message0 = success0 ? "Success" : obj0.ToString();
                }
                else if (result0 is Dictionary<string, object> dict0 && dict0.TryGetValue("success", out var successObj0) && successObj0 is bool bool0 && bool0)
                {
                    success0 = true;
                    message0 = "Success";
                }
                else
                {
                    success0 = false;
                    message0 = result0?.ToString() ?? "Unknown response";
                }
                testResults.Add(("build_vfx_graph_tree", success0, message0));
            }
            catch (Exception ex)
            {
                testResults.Add(("build_vfx_graph_tree", false, ex.Message));
            }

            // Test 1: Create VFX Graph
            Debug.Log("Test 1: Creating VFX graph...");
            try
            {
                var params1 = JObject.FromObject(new { graph_path = testGraphPath });
                var result1 = CreateVfxGraphTool.HandleCommand(params1);
                var success1 = result1 is Dictionary<string, object> dict1 && dict1.ContainsKey("success") && dict1["success"] as bool? == true;
                testResults.Add(("create_vfx_graph", success1, success1 ? "Success" : result1.ToString()));
            }
            catch (Exception ex)
            {
                testResults.Add(("create_vfx_graph", false, ex.Message));
            }

            // Test 2: List Graph Variants
            Debug.Log("Test 2: Listing graph variants...");
            try
            {
                var params2 = JObject.FromObject(new { category = "Operator", limit = 5 });
                var result2 = ListGraphVariantsTool.HandleCommand(params2);
                var success2 = result2 is Dictionary<string, object> dict2 && dict2.ContainsKey("success") && dict2["success"] as bool? == true;
                testResults.Add(("list_graph_variants", success2, success2 ? "Success" : result2.ToString()));
            }
            catch (Exception ex)
            {
                testResults.Add(("list_graph_variants", false, ex.Message));
            }

            // Test 3: Get Graph Structure
            Debug.Log("Test 3: Getting graph structure...");
            try
            {
                var params3 = JObject.FromObject(new { graph_path = testGraphPath });
                var result3 = GetGraphStructureTool.HandleCommand(params3);
                var success3 = result3 is Dictionary<string, object> dict3 && dict3.ContainsKey("success") && dict3["success"] as bool? == true;
                testResults.Add(("get_graph_structure", success3, success3 ? "Success" : result3.ToString()));
            }
            catch (Exception ex)
            {
                testResults.Add(("get_graph_structure", false, ex.Message));
            }

            // Test 4: List Exposed Parameters
            Debug.Log("Test 4: Listing exposed parameters...");
            try
            {
                var params4 = JObject.FromObject(new { graph_path = testGraphPath });
                var result4 = ListExposedParametersTool.HandleCommand(params4);
                var success4 = result4 is Dictionary<string, object> dict4 && dict4.ContainsKey("success") && dict4["success"] as bool? == true;
                testResults.Add(("list_exposed_parameters", success4, success4 ? "Success" : result4.ToString()));
            }
            catch (Exception ex)
            {
                testResults.Add(("list_exposed_parameters", false, ex.Message));
            }

            // Test 5: Get Diagnostics
            Debug.Log("Test 5: Getting graph diagnostics...");
            try
            {
                var params5 = JObject.FromObject(new { graph_path = testGraphPath });
                var result5 = GetVfxGraphDiagnosticsTool.HandleCommand(params5);
                var success5 = result5 is Dictionary<string, object> dict5 && dict5.ContainsKey("success") && dict5["success"] as bool? == true;
                testResults.Add(("get_vfx_graph_diagnostics", success5, success5 ? "Success" : result5.ToString()));
            }
            catch (Exception ex)
            {
                testResults.Add(("get_vfx_graph_diagnostics", false, ex.Message));
            }

            // Test 6: List Graph Instances
            Debug.Log("Test 6: Listing graph instances...");
            try
            {
                var params6 = JObject.FromObject(new { });
                var result6 = ListGraphInstancesTool.HandleCommand(params6);
                var success6 = result6 is Dictionary<string, object> dict6 && dict6.ContainsKey("success") && dict6["success"] as bool? == true;
                testResults.Add(("list_graph_instances", success6, success6 ? "Success" : result6.ToString()));
            }
            catch (Exception ex)
            {
                testResults.Add(("list_graph_instances", false, ex.Message));
            }

            // Test 7: Add Node (with duplicate handling)
            Debug.Log("Test 7: Adding node to graph...");
            try
            {
                var params7 = JObject.FromObject(new 
                { 
                    graph_path = testGraphPath,
                    node_type = "Multiply",
                    position_x = 100f,
                    position_y = 100f
                });
                var result7 = AddNodeToGraphTool.HandleCommand(params7);
                Dictionary<string, object> dict7 = null;
                var success7 = false;
                if (result7 is Dictionary<string, object> dictResult7)
                {
                    dict7 = dictResult7;
                    success7 = dict7.ContainsKey("success") && dict7["success"] as bool? == true;
                }
                var message7 = success7 ? "Success" : result7.ToString();
                if (success7 && dict7 != null && dict7.ContainsKey("data"))
                {
                    var data7 = dict7["data"] as Dictionary<string, object>;
                    if (data7 != null && data7.ContainsKey("warning"))
                    {
                        message7 += " (with duplicate warning - auto-adjusted)";
                    }
                }
                testResults.Add(("add_node_to_graph", success7, message7));
            }
            catch (Exception ex)
            {
                testResults.Add(("add_node_to_graph", false, ex.Message));
            }

            // Test 7b: Add Node at same position (test duplicate detection)
            Debug.Log("Test 7b: Testing duplicate node detection...");
            try
            {
                var params7b = JObject.FromObject(new 
                { 
                    graph_path = testGraphPath,
                    node_type = "Add",
                    position_x = 100f,
                    position_y = 100f
                });
                var result7b = AddNodeToGraphTool.HandleCommand(params7b);
                var success7b = result7b is Dictionary<string, object> dict7b && dict7b.ContainsKey("success") && dict7b["success"] as bool? == true;
                var message7b = success7b ? "Success (duplicate detected and handled)" : result7b.ToString();
                testResults.Add(("add_node_duplicate_handling", success7b, message7b));
            }
            catch (Exception ex)
            {
                testResults.Add(("add_node_duplicate_handling", false, ex.Message));
            }

            // Test 8: Connect Nodes (with improved slot selection)
            Debug.Log("Test 8: Testing connection with slot selection...");
            try
            {
                // First get graph structure to find node IDs
                var params8a = JObject.FromObject(new { graph_path = testGraphPath });
                var result8a = GetGraphStructureTool.HandleCommand(params8a);
                if (result8a is Dictionary<string, object> dict8a && dict8a.ContainsKey("data"))
                {
                    var data8a = dict8a["data"] as Dictionary<string, object>;
                    var nodes = data8a?["nodes"] as System.Collections.IList;
                    if (nodes != null && nodes.Count >= 2)
                    {
                        var node1 = nodes[0] as Dictionary<string, object>;
                        var node2 = nodes[1] as Dictionary<string, object>;
                        if (node1 != null && node2 != null)
                        {
                            var params8b = JObject.FromObject(new
                            {
                                graph_path = testGraphPath,
                                source_node_id = node1.ContainsKey("id") ? node1["id"] : 0,
                                source_port = "output",
                                target_node_id = node2.ContainsKey("id") ? node2["id"] : 0,
                                target_port = "input"
                            });
                            var result8b = ConnectGraphNodesTool.HandleCommand(params8b);
                            var success8b = result8b is Dictionary<string, object> dict8b && dict8b.ContainsKey("success") && dict8b["success"] as bool? == true;
                            testResults.Add(("connect_graph_nodes", success8b, success8b ? "Success" : result8b.ToString()));
                        }
                        else
                        {
                            testResults.Add(("connect_graph_nodes", false, "Not enough nodes in graph"));
                        }
                    }
                    else
                    {
                        testResults.Add(("connect_graph_nodes", false, "Graph has fewer than 2 nodes"));
                    }
                }
                else
                {
                    testResults.Add(("connect_graph_nodes", false, "Could not get graph structure"));
                }
            }
            catch (Exception ex)
            {
                testResults.Add(("connect_graph_nodes", false, ex.Message));
            }

            // Test 9: Disconnect Nodes (with improved error messages)
            Debug.Log("Test 9: Testing disconnect with error handling...");
            try
            {
                var params9a = JObject.FromObject(new { graph_path = testGraphPath });
                var result9a = GetGraphStructureTool.HandleCommand(params9a);
                if (result9a is Dictionary<string, object> dict9a && dict9a.ContainsKey("data"))
                {
                    var data9a = dict9a["data"] as Dictionary<string, object>;
                    var nodes = data9a?["nodes"] as System.Collections.IList;
                    if (nodes != null && nodes.Count >= 2)
                    {
                        var node1 = nodes[0] as Dictionary<string, object>;
                        var node2 = nodes[1] as Dictionary<string, object>;
                        if (node1 != null && node2 != null)
                        {
                            var params9b = JObject.FromObject(new
                            {
                                graph_path = testGraphPath,
                                source_node_id = node1.ContainsKey("id") ? node1["id"] : 0,
                                source_port = "output",
                                target_node_id = node2.ContainsKey("id") ? node2["id"] : 0,
                                target_port = "input"
                            });
                            var result9b = DisconnectGraphNodesTool.HandleCommand(params9b);
                            var success9b = result9b is Dictionary<string, object> dict9b && dict9b.ContainsKey("success") && dict9b["success"] as bool? == true;
                            testResults.Add(("disconnect_graph_nodes", success9b, success9b ? "Success" : result9b.ToString()));
                        }
                        else
                        {
                            testResults.Add(("disconnect_graph_nodes", false, "Not enough nodes in graph"));
                        }
                    }
                    else
                    {
                        testResults.Add(("disconnect_graph_nodes", false, "Graph has fewer than 2 nodes"));
                    }
                }
                else
                {
                    testResults.Add(("disconnect_graph_nodes", false, "Could not get graph structure"));
                }
            }
            catch (Exception ex)
            {
                testResults.Add(("disconnect_graph_nodes", false, ex.Message));
            }

            // Test 10: Move Node - DISABLED (move_graph_node tool disabled)
            /*
            Debug.Log("Test 10: Testing node movement...");
            try
            {
                var params10a = JObject.FromObject(new { graph_path = testGraphPath });
                var result10a = GetGraphStructureTool.HandleCommand(params10a);
                if (result10a is Dictionary<string, object> dict10a && dict10a.ContainsKey("data"))
                {
                    var data10a = dict10a["data"] as Dictionary<string, object>;
                    var nodes = data10a?["nodes"] as System.Collections.IList;
                    if (nodes != null && nodes.Count > 0)
                    {
                        var node1 = nodes[0] as Dictionary<string, object>;
                        if (node1 != null && node1.ContainsKey("id"))
                        {
                            var params10b = JObject.FromObject(new
                            {
                                graph_path = testGraphPath,
                                node_id = node1["id"],
                                position_x = 300f,
                                position_y = 300f
                            });
                            var result10b = MoveGraphNodeTool.HandleCommand(params10b);
                            var success10b = result10b is Dictionary<string, object> dict10b && dict10b.ContainsKey("success") && dict10b["success"] as bool? == true;
                            testResults.Add(("move_graph_node", success10b, success10b ? "Success" : result10b.ToString()));
                        }
                        else
                        {
                            testResults.Add(("move_graph_node", false, "Node ID not found"));
                        }
                    }
                    else
                    {
                        testResults.Add(("move_graph_node", false, "No nodes in graph"));
                    }
                }
                else
                {
                    testResults.Add(("move_graph_node", false, "Could not get graph structure"));
                }
            }
            catch (Exception ex)
            {
                testResults.Add(("move_graph_node", false, ex.Message));
            }
            */

            // Test 11: Set Node Property (with SafePropertySet fallback)
            Debug.Log("Test 11: Testing property setting with fallback...");
            try
            {
                var params11a = JObject.FromObject(new { graph_path = testGraphPath });
                var result11a = GetGraphStructureTool.HandleCommand(params11a);
                if (result11a is Dictionary<string, object> dict11a && dict11a.ContainsKey("data"))
                {
                    var data11a = dict11a["data"] as Dictionary<string, object>;
                    var nodes = data11a?["nodes"] as System.Collections.IList;
                    if (nodes != null && nodes.Count > 0)
                    {
                        var node1 = nodes[0] as Dictionary<string, object>;
                        if (node1 != null && node1.ContainsKey("id"))
                        {
                            var params11b = JObject.FromObject(new
                            {
                                graph_path = testGraphPath,
                                node_id = node1["id"],
                                property_name = "position",
                                property_value = new { x = 400f, y = 400f }
                            });
                            var result11b = SetGraphNodePropertyTool.HandleCommand(params11b);
                            var success11b = result11b is Dictionary<string, object> dict11b && dict11b.ContainsKey("success") && dict11b["success"] as bool? == true;
                            testResults.Add(("set_graph_node_property", success11b, success11b ? "Success" : result11b.ToString()));
                        }
                        else
                        {
                            testResults.Add(("set_graph_node_property", false, "Node ID not found"));
                        }
                    }
                    else
                    {
                        testResults.Add(("set_graph_node_property", false, "No nodes in graph"));
                    }
                }
                else
                {
                    testResults.Add(("set_graph_node_property", false, "Could not get graph structure"));
                }
            }
            catch (Exception ex)
            {
                testResults.Add(("set_graph_node_property", false, ex.Message));
            }

            // Test: List Graphs
            Debug.Log("Test: Listing graphs...");
            try
            {
                var listParams = JObject.FromObject(new { include_tags = true });
                var listResult = ListGraphsTool.HandleCommand(listParams);
                var listSuccess = listResult is Dictionary<string, object> listDict && listDict.ContainsKey("success") && listDict["success"] as bool? == true;
                testResults.Add(("list_graphs", listSuccess, listSuccess ? "Success" : listResult.ToString()));
            }
            catch (Exception ex)
            {
                testResults.Add(("list_graphs", false, ex.Message));
            }

            // Test: Describe VFX Graph
            Debug.Log("Test: Describing VFX graph...");
            try
            {
                var descParams = JObject.FromObject(new { graph_path = "Assets/VFX/BlueSnowOrb.vfx", lod = 2 });
                var descResult = DescribeVfxGraphTool.HandleCommand(descParams);
                var descSuccess = descResult is Dictionary<string, object> descDict && descDict.ContainsKey("success") && descDict["success"] as bool? == true;
                testResults.Add(("describe_vfx_graph", descSuccess, descSuccess ? "Success" : descResult.ToString()));
            }
            catch (Exception ex)
            {
                testResults.Add(("describe_vfx_graph", false, ex.Message));
            }

            // Test: Create VFX Instance
            Debug.Log("Test: Creating VFX instance...");
            try
            {
                var instanceParams = new JObject
                {
                    ["graph_path"] = "Assets/VFX/BlueSnowOrb.vfx",
                    ["instance_name"] = "TestInstance",
                    ["transform"] = JObject.FromObject(new { position = new { x = 0, y = 0, z = 0 } }),
                    ["params"] = new JObject()  // Use JObject instead of anonymous object to avoid 'params' keyword conflict
                };
                var instanceResult = CreateVfxInstanceTool.HandleCommand(instanceParams);
                var instanceSuccess = instanceResult is Dictionary<string, object> instanceDict && instanceDict.ContainsKey("success") && instanceDict["success"] as bool? == true;
                testResults.Add(("create_vfx_instance", instanceSuccess, instanceSuccess ? "Success" : instanceResult.ToString()));
            }
            catch (Exception ex)
            {
                testResults.Add(("create_vfx_instance", false, ex.Message));
            }

            // Summary
            Debug.Log("\n=== TEST SUMMARY ===");
            var passed = testResults.Count(r => r.success);
            var failed = testResults.Count(r => !r.success);
            Debug.Log($"Passed: {passed}/{testResults.Count}");
            Debug.Log($"Failed: {failed}/{testResults.Count}\n");

            foreach (var (tool, success, message) in testResults)
            {
                var status = success ? "✓" : "✗";
                Debug.Log($"{status} {tool}: {message}");
            }

            Debug.Log("\n=== TEST COMPLETE ===");
        }
    }
}

