using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Helper class for validating time control command authority in multiplayer scenarios.
    /// Currently returns true for all commands in single-player mode.
    /// In multiplayer, this will enforce server authority and player ownership rules.
    /// </summary>
    public static class TimeAuthorityValidator
    {
        /// <summary>
        /// Checks if a time control command is allowed for the given simulation mode and player.
        /// 
        /// SINGLE-PLAYER BEHAVIOR:
        /// - Always returns true (all commands allowed).
        /// 
        /// MULTIPLAYER BEHAVIOR (future):
        /// - Server: Returns true for all commands (server is authoritative).
        /// - Client: Returns true only for Player-scope commands with matching PlayerId.
        /// - Client: Returns false for Global-scope commands (server-only).
        /// - Client: Returns false for commands with invalid PlayerId.
        /// </summary>
        /// <param name="mode">Current simulation mode</param>
        /// <param name="cmd">Command to validate</param>
        /// <param name="localPlayerId">Local player's ID (0 in single-player)</param>
        /// <returns>True if command is allowed, false otherwise</returns>
        public static bool IsCommandAllowed(TimeSimulationMode mode, TimeControlCommand cmd, byte localPlayerId)
        {
            // Single-player: all commands allowed
            if (mode == TimeSimulationMode.SinglePlayer)
            {
                return true;
            }

            // Multiplayer: placeholder for future validation
            // TODO: In MP, validate:
            // - Server can issue any command
            // - Client can only issue Player-scope commands with matching PlayerId
            // - Client cannot issue Global-scope commands
            // - Commands with invalid PlayerId are rejected
            return true; // Placeholder - will be implemented when MP is added
        }

        /// <summary>
        /// Checks if a player can issue global time control commands.
        /// 
        /// SINGLE-PLAYER BEHAVIOR:
        /// - Always returns true.
        /// 
        /// MULTIPLAYER BEHAVIOR (future):
        /// - Server: Returns true (server is authoritative for global commands).
        /// - Client: Returns false (clients cannot issue global commands).
        /// </summary>
        /// <param name="mode">Current simulation mode</param>
        /// <param name="playerId">Player ID attempting to issue the command</param>
        /// <returns>True if player can issue global commands, false otherwise</returns>
        public static bool CanIssueGlobalCommand(TimeSimulationMode mode, byte playerId)
        {
            // Single-player: always allowed
            if (mode == TimeSimulationMode.SinglePlayer)
            {
                return true;
            }

            // Multiplayer: only server can issue global commands
            // TODO: In MP, check if playerId is server (typically playerId == 0 or special server ID)
            return false; // Placeholder - will be implemented when MP is added
        }

        /// <summary>
        /// Validates that a command's scope is valid for the current simulation mode.
        /// 
        /// SINGLE-PLAYER BEHAVIOR:
        /// - Global and LocalBubble scopes are valid.
        /// - Player scope is treated as Global (converted by RewindCoordinatorSystem).
        /// 
        /// MULTIPLAYER BEHAVIOR (future):
        /// - Global scope: Only valid for server.
        /// - Player scope: Valid for clients (must match their PlayerId).
        /// - LocalBubble scope: Valid for both server and clients.
        /// </summary>
        /// <param name="mode">Current simulation mode</param>
        /// <param name="scope">Command scope to validate</param>
        /// <returns>True if scope is valid for the mode, false otherwise</returns>
        public static bool IsScopeValid(TimeSimulationMode mode, TimeControlScope scope)
        {
            // Single-player: Global and LocalBubble are valid
            if (mode == TimeSimulationMode.SinglePlayer)
            {
                return scope == TimeControlScope.Global || scope == TimeControlScope.LocalBubble;
            }

            // Multiplayer: all scopes are potentially valid, but authority checks apply
            // TODO: In MP, validate scope based on command issuer (server vs client)
            return true; // Placeholder - will be implemented when MP is added
        }
    }
}


