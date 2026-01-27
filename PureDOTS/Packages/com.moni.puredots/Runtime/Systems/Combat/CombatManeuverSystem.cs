using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Selects and activates combat maneuvers based on pilot experience.
    /// Games define XP thresholds via VesselManeuverProfile; PureDOTS executes selection logic.
    /// Emits ManeuverStartEvent for game-side reactions (animations, sound, etc.).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatLoopSystem))]
    public partial struct CombatManeuverSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PilotExperience>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.DeltaTime;
            uint currentTick = timeState.Tick;

            foreach (var (experience, profile, loopState, entity) in SystemAPI
                         .Query<RefRO<PilotExperience>, RefRO<VesselManeuverProfile>, RefRW<CombatLoopState>>()
                         .WithEntityAccess())
            {
                var xp = experience.ValueRO.Experience;
                ref var loop = ref loopState.ValueRW;

                // Tick down current maneuver timer
                if (loop.ActiveManeuver != CombatManeuver.None)
                {
                    loop.ManeuverTimer -= deltaTime;
                    if (loop.ManeuverTimer <= 0f)
                    {
                        // Maneuver completed
                        loop.ActiveManeuver = CombatManeuver.None;
                        loop.ManeuverTimer = 0f;
                    }
                    continue; // Don't start new maneuver while one is active
                }

                // Only consider maneuvers during combat phases
                if (loop.Phase != CombatLoopPhase.Attack && loop.Phase != CombatLoopPhase.Intercept)
                {
                    continue;
                }

                // Select best available maneuver based on XP thresholds (game-defined)
                CombatManeuver selectedManeuver = CombatManeuver.None;
                float maneuverDuration = 0f;

                // Priority: JTurn > Kite > Strafe (higher skill = better maneuver)
                if (xp >= profile.ValueRO.JTurnThreshold)
                {
                    selectedManeuver = CombatManeuver.JTurn;
                    maneuverDuration = 2.0f; // JTurn takes 2 seconds
                }
                else if (xp >= profile.ValueRO.KiteThreshold)
                {
                    selectedManeuver = CombatManeuver.Kite;
                    maneuverDuration = 3.0f; // Kiting lasts 3 seconds
                }
                else if (xp >= profile.ValueRO.StrafeThreshold)
                {
                    selectedManeuver = CombatManeuver.Strafe;
                    maneuverDuration = 1.5f; // Strafe lasts 1.5 seconds
                }

                // Apply maneuver if one was selected
                if (selectedManeuver != CombatManeuver.None)
                {
                    // Use deterministic probability based on phase timer to avoid constant maneuvering
                    // Maneuver triggers when phase timer crosses certain thresholds
                    bool shouldTrigger = ShouldTriggerManeuver(loop.PhaseTimer, xp, currentTick, entity.Index);

                    if (shouldTrigger)
                    {
                        loop.ActiveManeuver = selectedManeuver;
                        loop.ManeuverTimer = maneuverDuration;
                        loop.ManeuverStartTick = currentTick;

                        // Emit event for game-side handling (animations, sound, etc.)
                        // Games can query ManeuverStartEvent buffer to react
                    }
                }
            }
        }

        /// <summary>
        /// Deterministic check for whether to trigger a maneuver.
        /// Uses phase timer and entity index for deterministic randomness.
        /// </summary>
        private static bool ShouldTriggerManeuver(float phaseTimer, float xp, uint tick, int entityIndex)
        {
            // Trigger maneuver every ~5 seconds of combat, with XP affecting frequency
            float interval = 5f - (xp * 0.02f); // Higher XP = more frequent maneuvers
            interval = interval < 2f ? 2f : interval; // Minimum 2 second cooldown

            // Deterministic "random" based on tick and entity
            uint hash = (tick + (uint)entityIndex * 31) % 1000;
            float probability = xp * 0.001f; // Higher XP = higher chance per tick

            // Check if we're at a trigger point
            float phaseTimerMod = phaseTimer % interval;
            bool atTriggerPoint = phaseTimerMod < 0.1f; // Within 0.1s of interval boundary

            return atTriggerPoint && (hash < probability * 1000);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
