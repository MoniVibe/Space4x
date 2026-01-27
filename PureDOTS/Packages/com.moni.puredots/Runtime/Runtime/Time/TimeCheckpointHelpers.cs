using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Helper utilities for working with time checkpoints (world snapshots).
    /// 
    /// CONCEPT: Snapshots are rare, coarse "checkpoints" that provide a baseline for rewinding.
    /// Per-component histories handle fine-grained playback between checkpoints.
    /// 
    /// MULTIPLAYER: This class reserves a place where MP will later constrain which checkpoints
    /// are allowed in netplay (e.g., only server-validated checkpoints, player-specific checkpoints, etc.).
    /// </summary>
    public static class TimeCheckpointHelpers
    {
        /// <summary>
        /// Validates whether a checkpoint (snapshot) is valid for use.
        /// 
        /// Single-player: Always returns true for valid snapshots.
        /// Multiplayer: Will later check owner/scope, server validation, etc.
        /// </summary>
        /// <param name="meta">Snapshot metadata to validate.</param>
        /// <returns>True if the checkpoint is valid for use.</returns>
        public static bool IsValidCheckpoint(in WorldSnapshotMeta meta)
        {
            // Basic validity check
            if (!meta.IsValid)
            {
                return false;
            }

            // For now: always true for valid snapshots
            // TODO: In multiplayer, add checks for:
            // - OwnerPlayerId matches requesting player (or is global)
            // - Scope is appropriate for the operation
            // - Server validation status
            // - Network synchronization state
            return true;
        }

        /// <summary>
        /// Checks if a checkpoint is a global snapshot (OwnerPlayerId = 0).
        /// </summary>
        /// <param name="meta">Snapshot metadata to check.</param>
        /// <returns>True if this is a global snapshot.</returns>
        public static bool IsGlobalCheckpoint(in WorldSnapshotMeta meta)
        {
            return meta.OwnerPlayerId == 0 && meta.Scope == TimeControlScope.Global;
        }

        /// <summary>
        /// Checks if a checkpoint belongs to a specific player.
        /// </summary>
        /// <param name="meta">Snapshot metadata to check.</param>
        /// <param name="playerId">Player ID to check against.</param>
        /// <returns>True if this checkpoint belongs to the specified player.</returns>
        public static bool IsPlayerCheckpoint(in WorldSnapshotMeta meta, byte playerId)
        {
            return meta.OwnerPlayerId == playerId && meta.Scope == TimeControlScope.Player;
        }

        /// <summary>
        /// Gets the checkpoint tick for a given simulation tick.
        /// Finds the nearest checkpoint at or before the target tick.
        /// </summary>
        /// <param name="targetTick">Target simulation tick.</param>
        /// <param name="checkpoints">Buffer of checkpoint metadata.</param>
        /// <param name="checkpointTick">Output: checkpoint tick if found.</param>
        /// <returns>True if a valid checkpoint was found.</returns>
        public static bool TryGetCheckpointTick(uint targetTick, 
            DynamicBuffer<WorldSnapshotMeta> checkpoints, 
            out uint checkpointTick)
        {
            checkpointTick = 0;
            
            if (checkpoints.Length == 0)
            {
                return false;
            }

            uint bestTick = 0;
            bool found = false;

            for (int i = 0; i < checkpoints.Length; i++)
            {
                var meta = checkpoints[i];
                if (IsValidCheckpoint(meta) && meta.Tick <= targetTick && meta.Tick > bestTick)
                {
                    bestTick = meta.Tick;
                    found = true;
                }
            }

            if (found)
            {
                checkpointTick = bestTick;
                return true;
            }

            return false;
        }
    }
}

