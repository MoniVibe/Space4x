using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("toggle_block_enabled")]
    public static class ToggleBlockEnabledTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var contextIdToken = @params["context_id"];
                var blockIdToken = @params["block_id"];
                var enabled = @params["enabled"]?.ToObject<bool?>();

                if (!TryParseContextId(contextIdToken, out var contextId, out var error) ||
                    !TryParseBlockId(blockIdToken, out var blockId, out error))
                {
                    return Response.Error(error);
                }

                if (!enabled.HasValue)
                {
                    return Response.Error("enabled is required (true or false)");
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

                var contextModel = VfxGraphReflectionHelpers.GetProperty(contextController, "model");
                if (contextModel == null)
                {
                    return Response.Error($"Could not get model for context {contextId}");
                }

                // Find the block
                object blockToToggle = null;
                foreach (var blockModel in VfxGraphReflectionHelpers.Enumerate(VfxGraphReflectionHelpers.GetProperty(contextModel, "children")))
                {
                    var unityObj = blockModel as UnityEngine.Object;
                    if (unityObj != null && unityObj.GetInstanceID() == blockId)
                    {
                        blockToToggle = blockModel;
                        break;
                    }
                }

                if (blockToToggle == null)
                {
                    return Response.Error($"Block with id {blockId} not found in context {contextId}");
                }

                // Try to set enabled property
                var blockType = blockToToggle.GetType();
                var enabledProperty = blockType.GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (enabledProperty != null && enabledProperty.CanWrite)
                {
                    enabledProperty.SetValue(blockToToggle, enabled.Value);
                }
                else
                {
                    // Try SetSettingValue as fallback
                    var setSettingMethod = blockType.GetMethod("SetSettingValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (setSettingMethod != null)
                    {
                        setSettingMethod.Invoke(blockToToggle, new object[] { "enabled", enabled.Value });
                    }
                    else
                    {
                        return Response.Error($"Block does not support enabled property");
                    }
                }

                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                return Response.Success($"Block {blockId} {(enabled.Value ? "enabled" : "disabled")} in context {contextId} in graph {graphPath}", new
                {
                    graphPath,
                    contextId,
                    blockId,
                    enabled = enabled.Value
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to toggle block enabled state: {ex.Message}");
            }
        }

        private static bool TryParseContextId(JToken token, out int id, out string error)
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

        private static bool TryParseBlockId(JToken token, out int id, out string error)
        {
            error = null;
            id = 0;

            if (token == null)
            {
                error = "block_id is required";
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

            error = $"Unable to parse block id '{asString}'";
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
    }
}

