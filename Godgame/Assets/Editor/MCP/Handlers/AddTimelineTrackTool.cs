#if false
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Timeline;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("add_timeline_track")]
    public static class AddTimelineTrackTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string timelinePath = @params["timeline_path"]?.ToString();
                string trackType = @params["track_type"]?.ToString() ?? "ActivationTrack";
                string trackName = @params["track_name"]?.ToString();
                
                if (string.IsNullOrEmpty(timelinePath))
                {
                    return Response.Error("timeline_path is required");
                }
                
                // Load timeline asset
                TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
                if (timeline == null)
                {
                    return Response.Error($"Timeline asset not found at {timelinePath}");
                }
                
                // Resolve track type
                Type type = null;
                switch (trackType.ToLower())
                {
                    case "activation":
                    case "activationtrack":
                        type = typeof(ActivationTrack);
                        break;
                    case "animation":
                    case "animationtrack":
                        type = typeof(AnimationTrack);
                        break;
                    case "audio":
                    case "audiotrack":
                        type = typeof(AudioTrack);
                        break;
                    case "control":
                    case "controltrack":
                        type = typeof(ControlTrack);
                        break;
                    case "playable":
                    case "playabletrack":
                        type = typeof(PlayableTrack);
                        break;
                    default:
                        return Response.Error($"Unknown track type: {trackType}. Supported types: ActivationTrack, AnimationTrack, AudioTrack, ControlTrack, PlayableTrack");
                }
                
                // Create track
                TrackAsset track = timeline.CreateTrack(type, null, trackName);
                if (track == null)
                {
                    return Response.Error($"Failed to create {trackType} track");
                }
                
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();
                
                return Response.Success($"Track added successfully", new
                {
                    timelinePath = timelinePath,
                    trackType = trackType,
                    trackName = track.name,
                    trackCount = timeline.outputTrackCount
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to add timeline track: {ex.Message}");
            }
        }
    }
}
#endif
