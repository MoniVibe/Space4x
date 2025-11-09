#if false
// Move tools disabled
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
    [McpForUnityTool("move_block_in_context")]
    public static class MoveBlockInContextTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var contextIdToken = @params["context_id"];
                var blockIdToken = @params["block_id"];
                var newIndex = @params["new_index"]?.ToObject<int?>();

                if (!newIndex.HasValue)
                {
                    return Response.Error("new_index is required");
                }

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

                // Move block
                var moved = MoveBlockToIndex(model, block, newIndex.Value);
                if (!moved)
                {
                    return Response.Error("Failed to move block in context");
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                controller.GetType().GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                EditorUtility.SetDirty(resource as UnityEngine.Object);
                VfxGraphReflectionHelpers.WriteAssetWithSubAssets(resource);
                AssetDatabase.SaveAssets();

                return Response.Success($"Block {blockId} moved to index {newIndex.Value} in context", new
                {
                    graphPath,
                    contextId,
                    blockId,
                    newIndex = newIndex.Value
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to move block in context: {ex.Message}");
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

        private static bool MoveBlockToIndex(object contextModel, object block, int targetIndex)
        {
            var contextType = contextModel.GetType();
            
            // Try MoveChild method
            var moveChildMethod = contextType.GetMethod("MoveChild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (moveChildMethod != null)
            {
                try
                {
                    moveChildMethod.Invoke(contextModel, new object[] { block, targetIndex });
                    return true;
                }
                catch { }
            }

            // Try RemoveAt + InsertAt pattern
            var blocksProperty = contextType.GetProperty("children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? contextType.GetProperty("blocks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            if (blocksProperty != null)
            {
                var blocks = blocksProperty.GetValue(contextModel);
                if (blocks != null)
                {
                    var blocksList = VfxGraphReflectionHelpers.Enumerate(blocks).Cast<object>().ToList();
                    var currentIndex = blocksList.IndexOf(block);
                    if (currentIndex >= 0 && currentIndex != targetIndex)
                    {
                        var removeAtMethod = blocks.GetType().GetMethod("RemoveAt", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var insertMethod = blocks.GetType().GetMethod("Insert", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        
                        if (removeAtMethod != null && insertMethod != null)
                        {
                            try
                            {
                                removeAtMethod.Invoke(blocks, new object[] { currentIndex });
                                var clampedIndex = Mathf.Clamp(targetIndex, 0, blocksList.Count - 1);
                                insertMethod.Invoke(blocks, new object[] { clampedIndex, block });
                                return true;
                            }
                            catch { }
                        }
                    }
                }
            }

            return false;
        }
    }
}
#endif

