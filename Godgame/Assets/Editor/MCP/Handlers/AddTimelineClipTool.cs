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
    [McpForUnityTool("add_timeline_clip")]
    public static class AddTimelineClipTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string timelinePath = @params["timeline_path"]?.ToString();
                int trackIndex = @params["track_index"]?.ToObject<int>() ?? -1;
                string trackName = @params["track_name"]?.ToString();
                string clipAssetPath = @params["clip_asset_path"]?.ToString();
                double startTime = @params["start_time"]?.ToObject<double>() ?? 0.0;
                double duration = @params["duration"]?.ToObject<double>() ?? 1.0;
                
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
                
                // Find track
                TrackAsset track = null;
                if (trackIndex >= 0 && trackIndex < timeline.outputTrackCount)
                {
                    track = timeline.GetOutputTrack(trackIndex);
                }
                else if (!string.IsNullOrEmpty(trackName))
                {
                    track = timeline.GetOutputTracks().FirstOrDefault(t => t.name == trackName);
                }
                else
                {
                    return Response.Error("Either track_index or track_name must be provided");
                }
                
                if (track == null)
                {
                    return Response.Error($"Track not found (index: {trackIndex}, name: {trackName})");
                }
                
                // Create clip based on track type
                TimelineClip clip = null;
                
                if (track is AudioTrack audioTrack)
                {
                    if (string.IsNullOrEmpty(clipAssetPath))
                    {
                        return Response.Error("clip_asset_path is required for AudioTrack (path to AudioClip)");
                    }
                    
                    AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipAssetPath);
                    if (audioClip == null)
                    {
                        return Response.Error($"AudioClip not found at {clipAssetPath}");
                    }
                    
                    clip = audioTrack.CreateClip(audioClip);
                    if (clip != null)
                    {
                        clip.start = startTime;
                        clip.duration = duration > 0 ? duration : audioClip.length;
                    }
                }
                else
                {
                    // For other track types, create a default clip
                    clip = track.CreateDefaultClip();
                    if (clip != null)
                    {
                        clip.start = startTime;
                        clip.duration = duration;
                    }
                }
                
                if (clip == null)
                {
                    return Response.Error("Failed to create clip");
                }
                
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();
                
                return Response.Success($"Clip added successfully", new
                {
                    timelinePath = timelinePath,
                    trackName = track.name,
                    clipStart = clip.start,
                    clipDuration = clip.duration,
                    clipAssetPath = clipAssetPath
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to add timeline clip: {ex.Message}");
            }
        }
    }
}
#endif
