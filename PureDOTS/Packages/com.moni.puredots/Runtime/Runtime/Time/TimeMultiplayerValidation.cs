namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Validation helpers for multiplayer time commands (stubs for future implementation).
    /// Currently no-op in single-player mode.
    /// </summary>
    public static class TimeMultiplayerValidation
    {
        /// <summary>
        /// Validates a time control command for multiplayer compatibility.
        /// In single-player mode, always returns true.
        /// </summary>
        /// <param name="mode">Current simulation mode.</param>
        /// <param name="cmd">Command to validate.</param>
        /// <param name="localPlayerId">Local player ID (for client validation).</param>
        /// <returns>True if command is allowed, false otherwise.</returns>
        public static bool IsCommandAllowed(TimeSimulationMode mode, TimeControlCommand cmd, byte localPlayerId)
        {
            // Single-player: all commands allowed
            if (mode == TimeSimulationMode.SinglePlayer)
            {
                return true;
            }
            
            // Multiplayer: stub for future implementation
            // TODO: Implement validation logic when adding MP support
            // - Server validates PlayerId matches command issuer
            // - Client commands require server authority
            // - Commands with invalid PlayerId are rejected
            return true;
        }
        
        /// <summary>
        /// Checks if a player can issue global time commands.
        /// In single-player mode, always returns true.
        /// </summary>
        /// <param name="mode">Current simulation mode.</param>
        /// <param name="playerId">Player ID to check.</param>
        /// <returns>True if player can issue global commands.</returns>
        public static bool CanIssueGlobalCommand(TimeSimulationMode mode, byte playerId)
        {
            if (mode == TimeSimulationMode.SinglePlayer)
            {
                return true;
            }
            
            // Multiplayer: stub for future implementation
            // TODO: Only server or authorized players can issue global commands
            // - Server can always issue global commands
            // - Clients cannot issue global commands (must use Player scope)
            return false;
        }
        
        /// <summary>
        /// Checks if a command scope is valid for the current simulation mode.
        /// </summary>
        /// <param name="mode">Current simulation mode.</param>
        /// <param name="scope">Command scope to validate.</param>
        /// <returns>True if scope is valid for the current mode.</returns>
        public static bool IsScopeValid(TimeSimulationMode mode, TimeControlScope scope)
        {
            if (mode == TimeSimulationMode.SinglePlayer)
            {
                // SP: Global and LocalBubble allowed, Player scope should be treated as Global
                return scope == TimeControlScope.Global || scope == TimeControlScope.LocalBubble;
            }
            
            // Multiplayer: stub for future implementation
            // TODO: Player and LocalBubble allowed, Global limited/disabled
            // - Server can use Global scope (with restrictions)
            // - Clients use Player scope for their own entities
            // - LocalBubble works for both server and clients
            return true;
        }
    }
}

