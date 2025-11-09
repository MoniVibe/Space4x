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
    [McpForUnityTool("remove_block_from_context")]
    public static class RemoveBlockFromContextTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var contextIdToken = @params["context_id"];
                var blockIdToken = @params["block_id"];

                if (!TryParseNodeId(contextIdToken, out var contextId, out var error) ||
                    !TryParseNodeId(blockIdToken, out var blockId, out error))
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
                controller.GetType().GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

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

                var block = FindBlockInContext(model, blockId);
                if (block == null)
                {
                    return Response.Error($"Block with id {blockId} not found in context");
                }

                // Remove block
                var removed = RemoveBlockFromContext(model, block);
                if (!removed)
                {
                    return Response.Error("Failed to remove block from context");
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                controller.GetType().GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                EditorUtility.SetDirty(resource as UnityEngine.Object);
                VfxGraphReflectionHelpers.WriteAssetWithSubAssets(resource);
                AssetDatabase.SaveAssets();

                return Response.Success($"Block {blockId} removed from context", new
                {
                    graphPath,
                    contextId,
                    blockId
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to remove block from context: {ex.Message}");
            }
        }

        private static bool TryParseNodeId(JToken token, out int id, out string error)
        {
            error = null;
            id = 0;

            if (token == null)
            {
                error = "context_id and block_id are required";
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

            error = $"Unable to parse id '{asString}'";
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

        private static object FindBlockInContext(object contextModel, int blockId)
        {
            var blocks = GetContextBlocks(contextModel);
            foreach (var block in blocks)
            {
                var unityObj = block as UnityEngine.Object;
                if (unityObj != null && unityObj.GetInstanceID() == blockId)
                {
                    return block;
                }
            }
            return null;
        }

        private static IEnumerable<object> GetContextBlocks(object contextModel)
        {
            var contextType = contextModel.GetType();
            
            var blocksProperty = contextType.GetProperty("children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? contextType.GetProperty("blocks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? contextType.GetProperty("m_Children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (blocksProperty != null)
            {
                var blocks = blocksProperty.GetValue(contextModel);
                if (blocks is IEnumerable enumerable)
                {
                    return VfxGraphReflectionHelpers.Enumerate(enumerable).Cast<object>();
                }
            }

            var getChildrenMethod = contextType.GetMethod("GetChildren", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getChildrenMethod != null)
            {
                var result = getChildrenMethod.Invoke(contextModel, null);
                if (result is IEnumerable enumerable)
                {
                    return VfxGraphReflectionHelpers.Enumerate(enumerable).Cast<object>();
                }
            }

            return Array.Empty<object>();
        }

        private static bool RemoveBlockFromContext(object contextModel, object block)
        {
            var contextType = contextModel.GetType();
            
            // Try RemoveChild method
            var removeChildMethod = contextType.GetMethod("RemoveChild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (removeChildMethod != null)
            {
                try
                {
                    removeChildMethod.Invoke(contextModel, new[] { block });
                    return true;
                }
                catch { }
            }

            // Try Remove method on blocks collection
            var blocksProperty = contextType.GetProperty("children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? contextType.GetProperty("blocks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            if (blocksProperty != null)
            {
                var blocks = blocksProperty.GetValue(contextModel);
                if (blocks != null)
                {
                    var removeMethod = blocks.GetType().GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (removeMethod != null)
                    {
                        try
                        {
                            removeMethod.Invoke(blocks, new[] { block });
                            return true;
                        }
                        catch { }
                    }
                }
            }

            return false;
        }
    }
}

