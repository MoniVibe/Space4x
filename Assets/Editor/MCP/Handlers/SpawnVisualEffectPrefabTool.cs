using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("spawn_visual_effect_prefab")]
    public static class SpawnVisualEffectPrefabTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var prefabPath = @params["prefab_path"]?.ToString();
                var positionX = @params["position_x"]?.ToObject<float?>() ?? 0f;
                var positionY = @params["position_y"]?.ToObject<float?>() ?? 0f;
                var positionZ = @params["position_z"]?.ToObject<float?>() ?? 0f;
                var parentName = @params["parent_name"]?.ToString();

                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    return Response.Error("prefab_path is required");
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    return Response.Error($"Prefab not found at path: {prefabPath}");
                }

                var position = new Vector3(positionX, positionY, positionZ);
                GameObject parent = null;
                if (!string.IsNullOrWhiteSpace(parentName))
                {
                    parent = GameObject.Find(parentName);
                }

                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    return Response.Error("Failed to instantiate prefab");
                }

                instance.transform.position = position;
                if (parent != null)
                {
                    instance.transform.SetParent(parent.transform);
                }

                var visualEffect = instance.GetComponent<UnityEngine.VFX.VisualEffect>();
                var hasVfx = visualEffect != null;

                Undo.RegisterCreatedObjectUndo(instance, "Spawn Visual Effect Prefab");
                Selection.activeGameObject = instance;

                return Response.Success($"Visual effect prefab spawned at {prefabPath}", new
                {
                    prefabPath,
                    instanceName = instance.name,
                    instanceId = instance.GetInstanceID(),
                    position = new { x = positionX, y = positionY, z = positionZ },
                    hasVisualEffect = hasVfx,
                    graphPath = visualEffect?.visualEffectAsset != null ? AssetDatabase.GetAssetPath(visualEffect.visualEffectAsset) : null
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to spawn visual effect prefab: {ex.Message}");
            }
        }
    }
}

