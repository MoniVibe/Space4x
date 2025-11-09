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
    [McpForUnityTool("play_audio_clip")]
    public static class PlayAudioClipTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string gameObjectName = @params["gameobject_name"]?.ToString();
                string clipPath = @params["clip_path"]?.ToString();
                string searchMethod = @params["search_method"]?.ToString() ?? "by_name";
                float volume = @params["volume"]?.ToObject<float>() ?? 1.0f;
                float pitch = @params["pitch"]?.ToObject<float>() ?? 1.0f;
                
                if (string.IsNullOrEmpty(gameObjectName))
                {
                    return Response.Error("gameobject_name is required (GameObject with AudioSource component)");
                }
                
                if (string.IsNullOrEmpty(clipPath))
                {
                    return Response.Error("clip_path is required (path to AudioClip asset)");
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
                
                // Get or add AudioSource component
                AudioSource audioSource = targetGO.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = targetGO.AddComponent<AudioSource>();
                }
                
                // Load audio clip
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip == null)
                {
                    return Response.Error($"AudioClip not found at {clipPath}");
                }
                
                // Configure and play
                audioSource.clip = clip;
                audioSource.volume = Mathf.Clamp01(volume);
                audioSource.pitch = Mathf.Clamp(pitch, -3.0f, 3.0f);
                
                // Note: In editor mode, we can't actually play audio, but we can set it up
                // The audio will play when entering play mode if PlayOnAwake is enabled
                // For actual playback, the user needs to enter play mode
                
                EditorUtility.SetDirty(targetGO);
                
                return Response.Success($"Audio clip configured on {targetGO.name}", new
                {
                    gameObject = targetGO.name,
                    clipPath = clipPath,
                    clipName = clip.name,
                    clipLength = clip.length,
                    volume = volume,
                    pitch = pitch,
                    note = "Audio will play when entering Play mode if PlayOnAwake is enabled"
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to play audio clip: {ex.Message}");
            }
        }
    }
}
#endif

