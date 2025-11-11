using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("list_context_blocks")]
    public static class ListContextBlocksTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var contextIdToken = @params["context_id"];

                if (!TryParseNodeId(contextIdToken, out var contextId, out var error))
                {
                    return Response.Error(error);
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out error))
                {
                    return Response.Error(error);
                }

                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out error))
                {
                    return Response.Error(error);
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                var syncArgs = new object[] { false };
                var syncMethod = controller.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "SyncControllerFromModel" && m.GetParameters().Length == 1);
                syncMethod?.Invoke(controller, syncArgs);

                var contextMap = BuildContextMap(controller);

                if (!contextMap.TryGetValue(contextId, out var contextController))
                {
                    return Response.Error($"Context with id {contextId} not found");
                }

                var model = VfxGraphReflectionHelpers.GetProperty(contextController, "model") as UnityEngine.Object;
                if (model == null)
                {
                    return Response.Error($"Context controller found but model is null");
                }

                var blocks = GetContextBlocks(model);
                var blockInfos = new List<Dictionary<string, object>>();

                foreach (var block in blocks)
                {
                    var blockType = block.GetType();
                    var blockInfo = new Dictionary<string, object>
                    {
                        ["type"] = blockType.FullName,
                        ["name"] = blockType.Name
                    };

                    // Try to get block name or title (handle ambiguous matches)
                    var nameProperties = blockType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(p => p.Name == "name" && p.CanRead)
                        .ToArray();
                    if (nameProperties.Length > 0)
                    {
                        try
                        {
                            var name = nameProperties[0].GetValue(block) as string;
                            if (!string.IsNullOrEmpty(name))
                            {
                                blockInfo["name"] = name;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[MCP Tools] list_context_blocks: Failed to get block name: {ex.Message}");
                        }
                    }

                    // Get block index/position if available (handle ambiguous matches)
                    var indexProperties = blockType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(p => p.Name == "index" && p.CanRead)
                        .ToArray();
                    if (indexProperties.Length > 0)
                    {
                        try
                        {
                            blockInfo["index"] = indexProperties[0].GetValue(block);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[MCP Tools] list_context_blocks: Failed to get block index: {ex.Message}");
                        }
                    }

                    var unityObj = block as UnityEngine.Object;
                    if (unityObj != null)
                    {
                        blockInfo["id"] = unityObj.GetInstanceID();
                    }

                    blockInfos.Add(blockInfo);
                }

                return Response.Success($"Found {blockInfos.Count} blocks in context", new
                {
                    graphPath,
                    contextId,
                    blockCount = blockInfos.Count,
                    blocks = blockInfos
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to list context blocks: {ex.Message}");
            }
        }

        private static bool TryParseNodeId(JToken token, out int id, out string error)
        {
            error = null;
            id = 0;

            if (token == null)
            {
                error = "context_id is required";
                return false;
            }

            if (token.Type == JTokenType.Integer && token.ToObject<int?>() is int directId)
            {
                id = directId;
                return true;
            }

            var asString = token.ToString();
            if (int.TryParse(asString, out id))
            {
                return true;
            }

            error = $"Unable to parse context id '{asString}'";
            return false;
        }

        private static Dictionary<int, object> BuildContextMap(object controller)
        {
            var map = new Dictionary<int, object>();
            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model != null)
                {
                    var modelType = model.GetType();
                    var vfxContextType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContext");
                    if (vfxContextType != null && vfxContextType.IsAssignableFrom(modelType))
                    {
                        map[model.GetInstanceID()] = nodeController;
                    }
                }
            }
            return map;
        }

        private static IEnumerable<object> GetContextBlocks(object contextModel)
        {
            var contextType = contextModel.GetType();
            
            // Try different property names for blocks (handle ambiguous matches)
            var allProperties = contextType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            System.Reflection.PropertyInfo blocksProperty = null;
            
            foreach (var propName in new[] { "children", "blocks", "m_Children" })
            {
                var matchingProps = allProperties.Where(p => p.Name == propName && p.CanRead).ToArray();
                if (matchingProps.Length > 0)
                {
                    blocksProperty = matchingProps[0];
                    break;
                }
            }

            if (blocksProperty != null)
            {
                var blocks = blocksProperty.GetValue(contextModel);
                if (blocks is IEnumerable enumerable)
                {
                    return VfxGraphReflectionHelpers.Enumerate(enumerable).Cast<object>();
                }
            }

            // Try GetChildren method (prefer parameterless version to avoid ambiguity)
            var getChildrenMethods = contextType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "GetChildren" && m.GetParameters().Length == 0)
                .ToArray();
            
            if (getChildrenMethods.Length > 0)
            {
                // Use the first parameterless GetChildren method
                var getChildrenMethod = getChildrenMethods[0];
                try
                {
                    var result = getChildrenMethod.Invoke(contextModel, null);
                    if (result is IEnumerable enumerable)
                    {
                        return VfxGraphReflectionHelpers.Enumerate(enumerable).Cast<object>();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] list_context_blocks: GetChildren() invocation failed: {ex.Message}");
                }
            }

            return Array.Empty<object>();
        }
    }
}

