using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Components;

namespace Space4X.Time
{
    /// <summary>
    /// Space4X-specific time control API.
    /// Extends the base TimeAPI with Space4X-specific helpers.
    /// </summary>
    public static class Space4XTimeAPI
    {
        /// <summary>
        /// Pauses the simulation.
        /// </summary>
        public static void Pause() => TimeAPI.Pause();

        /// <summary>
        /// Resumes the simulation.
        /// </summary>
        public static void Resume() => TimeAPI.Resume();

        /// <summary>
        /// Sets the simulation speed multiplier.
        /// </summary>
        /// <param name="speed">Speed multiplier (0.01-16.0)</param>
        public static void SetSpeed(float speed) => TimeAPI.SetSpeed(speed);

        /// <summary>
        /// Steps the simulation forward by one tick.
        /// </summary>
        public static void StepOneTick() => TimeAPI.StepOneTick();

        /// <summary>
        /// Gets the current simulation tick.
        /// </summary>
        public static uint GetCurrentTick() => TimeAPI.GetCurrentTick();

        /// <summary>
        /// Gets the current effective time scale.
        /// </summary>
        public static float GetCurrentScale() => TimeAPI.GetCurrentScale();

        /// <summary>
        /// Creates a stasis bubble at the specified position.
        /// </summary>
        /// <param name="position">Center position of the bubble</param>
        /// <param name="radius">Radius of the bubble</param>
        /// <param name="durationTicks">Duration in ticks (0 = permanent until removed)</param>
        /// <returns>Entity ID of the created bubble, or Entity.Null if creation failed</returns>
        public static Entity CreateStasisBubble(float3 position, float radius, uint durationTicks)
        {
            return TimeAPI.CreateStasisBubble(position, radius, durationTicks);
        }

        /// <summary>
        /// Requests a rewind of the local player region.
        /// </summary>
        /// <param name="lastNSeconds">Number of seconds to rewind</param>
        public static void RewindLocalPlayerRegion(float lastNSeconds)
        {
            TimeAPI.RewindLocalPlayerRegion(lastNSeconds);
        }

        /// <summary>
        /// Begins preview rewind - freezes world and starts scrubbing ghosts backwards.
        /// </summary>
        /// <param name="scrubSpeed">Rewind speed multiplier (1-4x)</param>
        public static void BeginRewindPreview(float scrubSpeed)
        {
            Debug.Log($"[Space4XTimeAPI] BeginRewindPreview({scrubSpeed:F2})");
            TimeAPI.BeginRewindPreview(scrubSpeed);
        }

        /// <summary>
        /// Updates the preview rewind scrub speed while scrubbing.
        /// </summary>
        /// <param name="scrubSpeed">New rewind speed multiplier (1-4x)</param>
        public static void UpdateRewindPreviewSpeed(float scrubSpeed)
        {
            Debug.Log($"[Space4XTimeAPI] UpdateRewindPreviewSpeed({scrubSpeed:F2})");
            TimeAPI.UpdateRewindPreviewSpeed(scrubSpeed);
        }

        /// <summary>
        /// Ends scrub preview - freezes ghosts at current preview position.
        /// </summary>
        public static void EndRewindScrub()
        {
            Debug.Log("[Space4XTimeAPI] EndRewindScrub()");
            TimeAPI.EndRewindScrub();
        }

        /// <summary>
        /// Commits rewind from preview - applies rewind to world state.
        /// </summary>
        public static void CommitRewindFromPreview()
        {
            Debug.Log("[Space4XTimeAPI] CommitRewindFromPreview()");
            TimeAPI.CommitRewindFromPreview();
        }

        /// <summary>
        /// Cancels rewind preview - aborts without changing world state.
        /// </summary>
        public static void CancelRewindPreview()
        {
            Debug.Log("[Space4XTimeAPI] CancelRewindPreview()");
            TimeAPI.CancelRewindPreview();
        }
    }
}


