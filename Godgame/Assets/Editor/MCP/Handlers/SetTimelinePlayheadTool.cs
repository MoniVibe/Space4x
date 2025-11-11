#if false
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("set_timeline_playhead")]
    public static class SetTimelinePlayheadTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string gameObjectName = @params["gameobject_name"]?.ToString();
                double time = @params["time"]?.ToObject<double>() ?? 0.0;
                string searchMethod = @params["search_method"]?.ToString() ?? "by_name";
                
                if (string.IsNullOrEmpty(gameObjectName))
                {
                    return Response.Error("gameobject_name is required (GameObject with PlayableDirector component)");
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
                
                // Get PlayableDirector component
                PlayableDirector director = targetGO.GetComponent<PlayableDirector>();
                if (director == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' does not have a PlayableDirector component");
                }
                
                if (director.playableAsset == null)
                {
                    return Response.Error($"PlayableDirector on '{gameObjectName}' has no timeline asset assigned");
                }
                
                // Set playhead time
                director.time = time;
                director.Evaluate();
                
                EditorUtility.SetDirty(targetGO);
                
                return Response.Success($"Playhead set to {time}", new
                {
                    gameObject = targetGO.name,
                    timelineAsset = director.playableAsset.name,
                    time = time,
                    duration = director.playableAsset is TimelineAsset ta ? ta.duration : 0.0
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to set timeline playhead: {ex.Message}");
            }
        }
    }
}
#endif
