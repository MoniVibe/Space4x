using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Marks time authority/ownership for entities in multiplayer.
    /// For now, this is a stub - no systems use it yet.
    /// </summary>
    public struct TimeAuthority : IComponentData
    {
        /// <summary>Owner player ID (-1 = neutral / world).</summary>
        public int OwnerPlayerId;
        /// <summary>Reserved for future partial authority (movement-only, economy-only, etc.).</summary>
        public byte AuthorityMask;
    }

    /// <summary>
    /// Per-player time state view (stub for future MP implementation).
    /// </summary>
    public struct PlayerTimeState : IComponentData
    {
        /// <summary>Player ID this state belongs to.</summary>
        public int PlayerId;
        /// <summary>Last acknowledged global tick (for future netcode sync).</summary>
        public uint LastAckedGlobalTick;
        /// <summary>Preferred local scale (UI hint, not authoritative).</summary>
        public float PreferredLocalScale;
    }
}

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Result of checking whether a time command is allowed.
    /// </summary>
    public enum TimeCommandAuthorityResult : byte
    {
        /// <summary>Command is allowed.</summary>
        Ok = 0,
        /// <summary>Command is denied.</summary>
        Denied = 1,
        /// <summary>Command is ignored in multiplayer mode.</summary>
        IgnoredInMP = 2
    }

    /// <summary>
    /// Validation helpers for multiplayer time commands (stubs for future implementation).
    /// </summary>
    public static class TimeMultiplayerGuards
    {
        /// <summary>
        /// Validates whether a time command would be allowed in the current multiplayer mode.
        /// </summary>
        /// <param name="flags">Time system feature flags.</param>
        /// <param name="cmd">Time control command to validate.</param>
        /// <param name="targetAuthority">Optional time authority for the target entity.</param>
        /// <returns>Result indicating whether the command is allowed.</returns>
        public static TimeCommandAuthorityResult CheckCommandAllowed(
            in TimeSystemFeatureFlags flags,
            in TimeControlCommand cmd,
            in TimeAuthority? targetAuthority)
        {
            if (!flags.IsMultiplayerSession)
                return TimeCommandAuthorityResult.Ok;

            switch (flags.MultiplayerMode)
            {
                case TimeMultiplayerMode.MP_DisableRewind:
                case TimeMultiplayerMode.MP_SnapshotsOnly:
                case TimeMultiplayerMode.MP_FullExperimental:
                    // For now: do not allow any time changing commands in MP.
                    return TimeCommandAuthorityResult.IgnoredInMP;

                default:
                    return TimeCommandAuthorityResult.IgnoredInMP;
            }
        }
    }
}

