using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("get_transform")]
    public static class GetTransformTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string gameObjectName = @params["gameobject_name"]?.ToString();
                string searchMethod = @params["search_method"]?.ToString() ?? "by_name";
                
                if (string.IsNullOrEmpty(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }
                
                // Find GameObject
                GameObject targetGO = null;
                if (searchMethod == "by_id" && int.TryParse(gameObjectName, out int instanceID))
                {
                    targetGO = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                }
                else
                {
                    targetGO = GameObject.Find(gameObjectName);
                    if (targetGO == null)
                    {
                        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                        targetGO = allObjects.FirstOrDefault(go => go.name == gameObjectName && go.scene.IsValid());
                    }
                }
                
                if (targetGO == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' not found");
                }
                
                Transform transform = targetGO.transform;
                
                // Get child names
                var children = new System.Collections.Generic.List<string>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    children.Add(transform.GetChild(i).name);
                }
                
                return Response.Success($"Transform retrieved for {targetGO.name}", new
                {
                    gameObject = targetGO.name,
                    instanceID = targetGO.GetInstanceID(),
                    position = new { x = transform.position.x, y = transform.position.y, z = transform.position.z },
                    localPosition = new { x = transform.localPosition.x, y = transform.localPosition.y, z = transform.localPosition.z },
                    rotation = new { x = transform.eulerAngles.x, y = transform.eulerAngles.y, z = transform.eulerAngles.z },
                    localRotation = new { x = transform.localEulerAngles.x, y = transform.localEulerAngles.y, z = transform.localEulerAngles.z },
                    scale = new { x = transform.localScale.x, y = transform.localScale.y, z = transform.localScale.z },
                    parent = transform.parent != null ? transform.parent.name : null,
                    childCount = transform.childCount,
                    children = children
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to get transform: {ex.Message}");
            }
        }
    }
}

