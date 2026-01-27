using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Static utility class for rewind track operations.
    /// Provides helpers for track config lookup, history trimming, and sampling decisions.
    /// </summary>
    [BurstCompile]
    public static class RewindUtil
    {
        /// <summary>
        /// Checks if a track should record a snapshot at the current tick based on RecordEveryTicks.
        /// </summary>
        public static bool ShouldRecordTrack(ref RewindConfigBlob config, RewindTrackId track, uint currentTick)
        {
            ref var trackDef = ref GetTrackDef(ref config, track);
            if (trackDef.RecordEveryTicks == 0)
                return false;
            return (currentTick % trackDef.RecordEveryTicks) == 0;
        }

        /// <summary>
        /// Gets the track definition for a given track ID from the config blob.
        /// Throws if track not found.
        /// </summary>
        public static ref RewindTrackDef GetTrackDef(ref RewindConfigBlob config, RewindTrackId id)
        {
            for (int i = 0; i < config.Tracks.Length; i++)
            {
                if (config.Tracks[i].Id.Value == id.Value)
                    return ref config.Tracks[i];
            }
            throw new System.Exception($"Rewind track {id.Value} not found in config.");
        }

        /// <summary>
        /// Tries to get the track definition for a given track ID.
        /// Returns true if found, false otherwise.
        /// </summary>
        public static bool TryGetTrackDef(ref RewindConfigBlob config, RewindTrackId id, out RewindTrackDef trackDef)
        {
            for (int i = 0; i < config.Tracks.Length; i++)
            {
                if (config.Tracks[i].Id.Value == id.Value)
                {
                    trackDef = config.Tracks[i];
                    return true;
                }
            }
            trackDef = default;
            return false;
        }

        /// <summary>
        /// Trims history buffer to respect window size.
        /// Removes all elements older than (currentTick - windowTicks).
        /// Assumes history elements have a Tick field accessible via UnsafeGetTick.
        /// </summary>
        public static void TrimHistory<T>(DynamicBuffer<T> history, uint currentTick, uint windowTicks) 
            where T : unmanaged, IBufferElementData
        {
            if (history.Length == 0)
                return;

            uint oldestAllowed = currentTick >= windowTicks ? currentTick - windowTicks : 0;
            
            int removeCount = 0;
            for (int i = 0; i < history.Length; i++)
            {
                uint elementTick = UnsafeGetTick(history[i]);
                if (elementTick < oldestAllowed)
                {
                    removeCount++;
                }
                else
                {
                    break;
                }
            }

            if (removeCount > 0)
            {
                history.RemoveRange(0, removeCount);
            }
        }

        /// <summary>
        /// Generic helper to extract tick from history element.
        /// Assumes Tick is the first field (common pattern for history elements).
        /// For custom layouts, provide explicit overloads or ensure Tick is first field.
        /// </summary>
        [BurstCompile]
        private static uint UnsafeGetTick<T>(T element) where T : unmanaged
        {
            // Common pattern: Tick is first field in history elements
            unsafe
            {
                var ptr = (byte*)&element;
                return *(uint*)ptr;
            }
        }
    }

    /// <summary>
    /// Interface for history elements that have a Tick field.
    /// Note: This interface is for documentation purposes only.
    /// RewindUtil.TrimHistory() uses unsafe access assuming Tick is the first field.
    /// Ensure Tick is the first field in your history element structs for compatibility.
    /// </summary>
    public interface IHistoryElementWithTick
    {
        uint Tick { get; }
    }
}

