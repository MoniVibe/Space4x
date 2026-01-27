using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using Unity.Entities;

namespace PureDOTS.Systems.Physics
{
    /// <summary>
    /// Static helper methods for physics and rewind integration.
    /// </summary>
    public static class PhysicsRewindHelper
    {
        /// <summary>
        /// Checks if we're in a post-rewind settle frame where collision events should be skipped.
        /// </summary>
        /// <param name="config">Physics configuration singleton.</param>
        /// <param name="currentTick">Current simulation tick.</param>
        /// <returns>True if this is a settle frame after rewind.</returns>
        public static bool IsPostRewindSettleFrame(in PhysicsConfig config, uint currentTick)
        {
            return PhysicsConfigHelpers.IsPostRewindSettleFrame(in config, currentTick);
        }

        /// <summary>
        /// Checks if physics should be skipped based on rewind state.
        /// </summary>
        /// <param name="rewindState">Current rewind state.</param>
        /// <returns>True if physics should be skipped.</returns>
        public static bool ShouldSkipPhysics(in RewindState rewindState)
        {
            // Skip physics during playback - ECS state is authoritative
            return rewindState.Mode == RewindMode.Playback;
        }

        /// <summary>
        /// Checks if collision events should be processed.
        /// </summary>
        /// <param name="config">Physics configuration.</param>
        /// <param name="rewindState">Current rewind state.</param>
        /// <param name="currentTick">Current simulation tick.</param>
        /// <returns>True if collision events should be processed.</returns>
        public static bool ShouldProcessCollisionEvents(
            in PhysicsConfig config,
            in RewindState rewindState,
            uint currentTick)
        {
            // Skip during playback
            if (rewindState.Mode == RewindMode.Playback)
            {
                return false;
            }

            // Skip during settle frames
            if (IsPostRewindSettleFrame(in config, currentTick))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the appropriate physics mode based on rewind state.
        /// </summary>
        /// <param name="rewindState">Current rewind state.</param>
        /// <returns>The physics mode to use.</returns>
        public static PhysicsMode GetPhysicsMode(in RewindState rewindState)
        {
            return rewindState.Mode switch
            {
                RewindMode.Play => PhysicsMode.Normal,
                RewindMode.Paused => PhysicsMode.Normal,
                RewindMode.Rewind => PhysicsMode.Disabled,
                RewindMode.Step => PhysicsMode.Normal,
                _ => PhysicsMode.Normal
            };
        }
    }

    /// <summary>
    /// Physics operation mode based on rewind state.
    /// </summary>
    public enum PhysicsMode
    {
        /// <summary>
        /// Normal physics operation - full collision detection and events.
        /// </summary>
        Normal,

        /// <summary>
        /// Physics disabled - during rewind playback.
        /// </summary>
        Disabled,

        /// <summary>
        /// Settling mode - physics runs but events may be skipped.
        /// Used during catch-up after rewind.
        /// </summary>
        Settling
    }
}

