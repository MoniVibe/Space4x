using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Static utility methods for working with the time system.
    /// All methods are Burst-compatible and designed for use in ISystem.OnUpdate.
    /// 
    /// DESIGN INVARIANT: This is the canonical source for all time calculations.
    /// DESIGN INVARIANT: All simulation systems should use these methods for time operations.
    /// DESIGN INVARIANT: GetEffectiveDelta is the correct way to obtain per-entity delta time (even without bubbles).
    /// DESIGN INVARIANT: ShouldUpdate is the canonical gate for update vs skip logic.
    /// 
    /// NOTE: Class-level [BurstCompile] removed to avoid Burst function pointer restrictions.
    /// Methods will still be inlined into Burst-compiled systems/jobs.
    /// </summary>
    public static class TimeHelpers
    {
        /// <summary>
        /// Gets the effective time delta for an entity, considering global time and local time bubble effects.
        /// 
        /// DESIGN INVARIANT: This is the ONLY correct way for simulation systems to obtain delta time.
        /// DESIGN INVARIANT: Handles global time, pause, and local bubble effects automatically.
        /// DESIGN INVARIANT: Returns 0 for paused/stasis entities, negative for rewind mode.
        /// 
        /// All simulation systems should use this method instead of directly accessing TimeState.DeltaTime.
        /// </summary>
        /// <param name="tickTimeState">Global tick time state.</param>
        /// <param name="timeState">Global time state.</param>
        /// <param name="membership">Entity's time bubble membership (can be default if not in a bubble).</param>
        /// <returns>Effective delta time for this entity's simulation.</returns>
        public static float GetEffectiveDelta(in TickTimeState tickTimeState, in TimeState timeState, 
            in TimeBubbleMembership membership)
        {
            // If entity is not in a bubble (BubbleId == 0), use global time
            if (membership.BubbleId == 0)
            {
                return GetGlobalDelta(tickTimeState, timeState);
            }

            // Handle local time modes
            switch (membership.LocalMode)
            {
                case TimeBubbleMode.Stasis:
                case TimeBubbleMode.Pause:
                    return 0f;

                case TimeBubbleMode.Scale:
                case TimeBubbleMode.FastForward:
                    return tickTimeState.FixedDeltaTime * membership.LocalScale;

                case TimeBubbleMode.Rewind:
                    // Negative delta for rewind - caller should handle playback
                    return -tickTimeState.FixedDeltaTime * math.abs(membership.LocalScale);

                default:
                    return GetGlobalDelta(tickTimeState, timeState);
            }
        }

        /// <summary>
        /// Gets the effective time delta without bubble membership lookup (assumes global time).
        /// </summary>
        public static float GetGlobalDelta(in TickTimeState tickTimeState, in TimeState timeState)
        {
            if (timeState.IsPaused)
            {
                return 0f;
            }
            return tickTimeState.FixedDeltaTime * timeState.CurrentSpeedMultiplier;
        }

        /// <summary>
        /// Gets the effective time scale for an entity.
        /// 
        /// SINGLE-PLAYER BEHAVIOR:
        /// - Returns global scale if entity is not in a bubble.
        /// - Returns bubble-local scale if entity is in a bubble.
        /// 
        /// MULTIPLAYER SEMANTICS (future):
        /// - Current behavior is SP-only (simulation effect).
        /// - Future MP will check TimeBubbleParams.AuthorityPolicy and TimeSystemFeatureFlags.IsMultiplayerSession.
        /// - Bubbles with AuthorityPolicy.SinglePlayerOnly will be ignored in MP.
        /// - Bubbles with AuthorityPolicy.LocalPlayerOnly will affect presentation only (client-side), not simulation.
        /// - Bubbles with AuthorityPolicy.AuthoritativeShared will affect simulation (server-authoritative).
        /// </summary>
        public static float GetEffectiveScale(in TimeState timeState, in TimeBubbleMembership membership)
        {
            if (membership.BubbleId == 0)
            {
                return timeState.CurrentSpeedMultiplier;
            }

            switch (membership.LocalMode)
            {
                case TimeBubbleMode.Stasis:
                case TimeBubbleMode.Pause:
                    return 0f;

                case TimeBubbleMode.Scale:
                case TimeBubbleMode.FastForward:
                    return membership.LocalScale;

                case TimeBubbleMode.Rewind:
                    return -math.abs(membership.LocalScale);

                default:
                    return timeState.CurrentSpeedMultiplier;
            }
        }

        /// <summary>
        /// Checks if an entity is currently in rewind mode (either global or local).
        /// 
        /// MULTIPLAYER SEMANTICS (future):
        /// - LocalMode == Rewind will not change global Tick; it will read from history for that entity only.
        /// - Each player's entities can have independent local rewind states without affecting other players.
        /// - Cross-owner interaction rules (combat, trades, projectiles) will be defined in higher-level systems, not here.
        /// </summary>
        /// <param name="rewindState">Global rewind state.</param>
        /// <param name="membership">Entity's time bubble membership (can be default if not in a bubble).</param>
        /// <returns>True if the entity is in rewind mode.</returns>
        public static bool IsInRewindMode(in RewindState rewindState, in TimeBubbleMembership membership)
        {
            // Check local rewind first
            if (membership.BubbleId != 0 && membership.LocalMode == TimeBubbleMode.Rewind)
            {
                return true;
            }

            // Check global rewind
            return rewindState.Mode == RewindMode.Playback;
        }

        /// <summary>
        /// Checks if an entity should be updated this tick (not paused, not in stasis).
        /// 
        /// DESIGN INVARIANT: This is the canonical gate for whether an entity should update this frame.
        /// DESIGN INVARIANT: Checks stasis, pause, and global pause states comprehensively.
        /// DESIGN INVARIANT: Use this instead of ad-hoc pause checks in systems.
        /// 
        /// All simulation systems should use this method to determine if an entity should be updated.
        /// </summary>
        /// <param name="timeState">Global time state.</param>
        /// <param name="rewindState">Global rewind state.</param>
        /// <param name="membership">Entity's time bubble membership (can be default if not in a bubble).</param>
        /// <returns>True if the entity should be updated this frame.</returns>
        public static bool ShouldUpdate(in TimeState timeState, in RewindState rewindState, 
            in TimeBubbleMembership membership)
        {
            // Check stasis first (complete freeze)
            if (membership.BubbleId != 0 && membership.LocalMode == TimeBubbleMode.Stasis)
            {
                return false;
            }

            // Check local pause
            if (membership.BubbleId != 0 && membership.LocalMode == TimeBubbleMode.Pause)
            {
                return false;
            }

            // Check global pause (but not during playback)
            if (timeState.IsPaused && rewindState.Mode == RewindMode.Record)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if an entity is in stasis (complete freeze with no updates).
        /// 
        /// SINGLE-PLAYER BEHAVIOR:
        /// - Returns true if entity is in a bubble with Stasis mode.
        /// 
        /// MULTIPLAYER SEMANTICS (future):
        /// - Current behavior is SP-only (simulation effect).
        /// - Future MP will check TimeBubbleParams.AuthorityPolicy and TimeSystemFeatureFlags.IsMultiplayerSession.
        /// - Bubbles with AuthorityPolicy.SinglePlayerOnly will be ignored in MP.
        /// - Bubbles with AuthorityPolicy.LocalPlayerOnly will affect presentation only (client-side), not simulation.
        /// - Bubbles with AuthorityPolicy.AuthoritativeShared will affect simulation (server-authoritative).
        /// </summary>
        public static bool IsInStasis(in TimeBubbleMembership membership)
        {
            return membership.BubbleId != 0 && membership.LocalMode == TimeBubbleMode.Stasis;
        }

        /// <summary>
        /// Gets the playback tick for an entity (either global or local).
        /// 
        /// SINGLE-PLAYER BEHAVIOR:
        /// - Returns local playback tick if entity is in a rewind bubble.
        /// - Returns global playback tick if in global rewind mode.
        /// - Returns current tick otherwise.
        /// 
        /// MULTIPLAYER SEMANTICS (future):
        /// - LocalMode == Rewind will not change global Tick; it will read from history for that entity only.
        /// - Each player's entities can have independent local playback ticks without affecting other players.
        /// - The global Tick continues to advance normally even when some entities are in local rewind.
        /// - Cross-owner interaction rules (combat, trades, projectiles) will be defined in higher-level systems, not here.
        /// - Future MP will check TimeBubbleParams.AuthorityPolicy and TimeSystemFeatureFlags.IsMultiplayerSession.
        /// - Bubbles with AuthorityPolicy.SinglePlayerOnly will be ignored in MP.
        /// - Bubbles with AuthorityPolicy.LocalPlayerOnly will affect presentation only (client-side), not simulation.
        /// - Bubbles with AuthorityPolicy.AuthoritativeShared will affect simulation (server-authoritative).
        /// </summary>
        /// <param name="timeState">Global time state.</param>
        /// <param name="rewindState">Global rewind state.</param>
        /// <param name="membership">Entity's time bubble membership (can be default if not in a bubble).</param>
        /// <returns>Playback tick for this entity (local rewind, global playback, or current tick).</returns>
        public static uint GetPlaybackTick(in TimeState timeState, in RewindState rewindState, 
            in TimeBubbleMembership membership)
        {
            // Local rewind takes precedence
            if (membership.BubbleId != 0 && membership.LocalMode == TimeBubbleMode.Rewind)
            {
                return membership.LocalPlaybackTick;
            }

            // Global playback
            if (rewindState.Mode == RewindMode.Playback)
            {
                return timeState.Tick;
            }

            // Normal simulation
            return timeState.Tick;
        }

        /// <summary>
        /// Calculates ticks from seconds using the tick rate.
        /// </summary>
        public static uint SecondsToTicks(float seconds, float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f)
            {
                return 0;
            }
            return (uint)math.max(0, math.round(seconds / fixedDeltaTime));
        }

        /// <summary>
        /// Calculates seconds from ticks using the tick rate.
        /// </summary>
        public static float TicksToSeconds(uint ticks, float fixedDeltaTime)
        {
            return ticks * fixedDeltaTime;
        }

        /// <summary>
        /// Clamps a speed multiplier to valid range.
        /// 
        /// DESIGN INVARIANT: Speed bounds are configured via ScriptableObjects (TimeConfigAsset/HistoryConfigAsset).
        /// DESIGN INVARIANT: Default range is 0.01-16.0, fully moddable via config assets.
        /// DESIGN INVARIANT: This is the ONLY place speed clamping should occur in the codebase.
        /// </summary>
        /// <param name="speed">Speed multiplier to clamp.</param>
        /// <param name="minSpeed">Minimum speed (default 0.01, should come from config).</param>
        /// <param name="maxSpeed">Maximum speed (default 16.0, should come from config).</param>
        /// <returns>Clamped speed multiplier.</returns>
        public static float ClampSpeed(float speed, float minSpeed = 0.01f, float maxSpeed = 16f)
        {
            return math.clamp(speed, minSpeed, maxSpeed);
        }

        /// <summary>
        /// Interpolates between two values based on time progression.
        /// </summary>
        public static float LerpByTime(float from, float to, float delta, float rate)
        {
            float t = 1f - math.pow(0.5f, delta * rate);
            return math.lerp(from, to, t);
        }

        /// <summary>
        /// Gets a deterministic hash from tick and entity for random operations.
        /// </summary>
        public static uint GetDeterministicSeed(uint tick, int entityIndex, uint salt = 0)
        {
            // Simple hash combining tick, entity, and salt
            uint hash = tick;
            hash = hash * 31 + (uint)entityIndex;
            hash = hash * 31 + salt;
            hash ^= hash >> 16;
            hash *= 0x85ebca6b;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35;
            hash ^= hash >> 16;
            return hash;
        }
    }

    /// <summary>
    /// Extensions for common time operations on entities.
    /// </summary>
    public static class TimeHelpersExtensions
    {
        /// <summary>
        /// Gets effective delta for an entity, handling the case where TimeBubbleMembership might not exist.
        /// </summary>
        public static float GetEffectiveDelta(ref SystemState state, Entity entity, 
            in TickTimeState tickTimeState, in TimeState timeState)
        {
            var membership = state.EntityManager.HasComponent<TimeBubbleMembership>(entity)
                ? state.EntityManager.GetComponentData<TimeBubbleMembership>(entity)
                : default;

            return TimeHelpers.GetEffectiveDelta(tickTimeState, timeState, membership);
        }

        /// <summary>
        /// Checks if an entity should be updated, handling the case where TimeBubbleMembership might not exist.
        /// </summary>
        public static bool ShouldUpdate(ref SystemState state, Entity entity,
            in TimeState timeState, in RewindState rewindState)
        {
            // Check for StasisTag first (faster check)
            if (state.EntityManager.HasComponent<StasisTag>(entity))
            {
                return false;
            }

            var membership = state.EntityManager.HasComponent<TimeBubbleMembership>(entity)
                ? state.EntityManager.GetComponentData<TimeBubbleMembership>(entity)
                : default;

            return TimeHelpers.ShouldUpdate(timeState, rewindState, membership);
        }
    }
}
