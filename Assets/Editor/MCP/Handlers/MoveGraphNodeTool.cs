#if false
// Move tools disabled
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
    [McpForUnityTool("move_graph_node")]
    public static class MoveGraphNodeTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var nodeIdToken = @params["node_id"];
                var positionX = @params["position_x"]?.ToObject<float?>();
                var positionY = @params["position_y"]?.ToObject<float?>();

                if (!positionX.HasValue || !positionY.HasValue)
                {
                    return Response.Error("position_x and position_y are required");
                }

                if (!TryParseNodeId(nodeIdToken, out var nodeId, out var error))
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

                var nodeMap = BuildNodeMap(controller);

                if (!nodeMap.TryGetValue(nodeId, out var nodeController))
                {
                    return Response.Error($"Node with id {nodeId} not found");
                }

                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model == null)
                {
                    return Response.Error($"Node controller found but model is null");
                }

                var newPosition = new Vector2(positionX.Value, positionY.Value);
                var modelType = model.GetType();
                var positionProperty = modelType.GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (positionProperty == null)
                {
                    return Response.Error("Node model does not have a position property");
                }

                var oldPosition = positionProperty.GetValue(model);
                positionProperty.SetValue(model, newPosition);

                // Also try to update via controller if there's a method
                var setPositionMethod = nodeController.GetType().GetMethod("SetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (setPositionMethod != null)
                {
                    try
                    {
                        setPositionMethod.Invoke(nodeController, new object[] { newPosition });
                    }
                    catch
                    {
                        // Ignore if method signature doesn't match
                    }
                }

                // Use SyncAndSave helper for safe, guarded asset saving
                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                return Response.Success($"Node {nodeId} moved to ({newPosition.x}, {newPosition.y})", new
                {
                    graphPath,
                    nodeId,
                    oldPosition = oldPosition is Vector2 oldPos ? new { x = oldPos.x, y = oldPos.y } : null,
                    newPosition = new { x = newPosition.x, y = newPosition.y }
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to move graph node: {ex.Message}");
            }
        }

        private static bool TryParseNodeId(JToken token, out int id, out string error)
        {
            error = null;
            id = 0;

            if (token == null)
            {
                error = "node_id is required";
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

            error = $"Unable to parse node id '{asString}'";
            return false;
        }

        private static Dictionary<int, object> BuildNodeMap(object controller)
        {
            var map = new Dictionary<int, object>();
            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model != null)
                {
                    map[model.GetInstanceID()] = nodeController;
                }
            }
            return map;
        }
    }
}
#endif

