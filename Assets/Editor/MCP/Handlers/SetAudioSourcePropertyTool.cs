#if false
// Audio tools disabled
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("set_audio_source_property")]
    public static class SetAudioSourcePropertyTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string gameObjectName = @params["gameobject_name"]?.ToString();
                string searchMethod = @params["search_method"]?.ToString() ?? "by_name";
                float? volume = @params["volume"]?.ToObject<float>();
                float? pitch = @params["pitch"]?.ToObject<float>();
                bool? playOnAwake = @params["play_on_awake"]?.ToObject<bool>();
                bool? loop = @params["loop"]?.ToObject<bool>();
                
                if (string.IsNullOrEmpty(gameObjectName))
                {
                    return Response.Error("gameobject_name is required (GameObject with AudioSource component)");
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
                
                // Get AudioSource component
                AudioSource audioSource = targetGO.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' does not have an AudioSource component");
                }
                
                bool changed = false;
                
                // Set properties
                if (volume.HasValue)
                {
                    audioSource.volume = Mathf.Clamp01(volume.Value);
                    changed = true;
                }
                
                if (pitch.HasValue)
                {
                    audioSource.pitch = Mathf.Clamp(pitch.Value, -3.0f, 3.0f);
                    changed = true;
                }
                
                if (playOnAwake.HasValue)
                {
                    audioSource.playOnAwake = playOnAwake.Value;
                    changed = true;
                }
                
                if (loop.HasValue)
                {
                    audioSource.loop = loop.Value;
                    changed = true;
                }
                
                if (!changed)
                {
                    return Response.Error("No properties specified. Provide volume, pitch, play_on_awake, or loop.");
                }
                
                EditorUtility.SetDirty(targetGO);
                
                return Response.Success($"AudioSource properties updated on {targetGO.name}", new
                {
                    gameObject = targetGO.name,
                    volume = audioSource.volume,
                    pitch = audioSource.pitch,
                    playOnAwake = audioSource.playOnAwake,
                    loop = audioSource.loop
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to set audio source property: {ex.Message}");
            }
        }
    }
}
#endif

