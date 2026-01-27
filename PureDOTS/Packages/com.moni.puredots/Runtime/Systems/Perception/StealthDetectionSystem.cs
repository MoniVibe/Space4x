using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Detection;
using PureDOTS.Runtime.Movement;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Perception
{
    /// <summary>
    /// Updates stealth modifiers based on environmental context (light, terrain, movement).
    /// Runs throttled (every N ticks) to avoid per-frame overhead.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateBefore(typeof(PerceptionUpdateSystem))]
    public partial struct StealthModifierUpdateSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<StealthStats> _stealthStatsLookup;
        private ComponentLookup<MovementState> _movementLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SimulationFeatureFlags>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _stealthStatsLookup = state.GetComponentLookup<StealthStats>(true);
            _movementLookup = state.GetComponentLookup<MovementState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.PerceptionEnabled) == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _stealthStatsLookup.Update(ref state);
            _movementLookup.Update(ref state);

            // Update modifiers every 10 ticks (throttled)
            var updateInterval = 10u;
            var currentTick = timeState.Tick;

            foreach (var (modifiers, transform, entity) in SystemAPI.Query<RefRW<StealthModifiers>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Check if update needed
                var ticksSinceUpdate = currentTick - modifiers.ValueRO.LastUpdateTick;
                if (ticksSinceUpdate < updateInterval)
                {
                    continue;
                }

                // Get movement speed from MovementState if available
                var movementSpeed = 0f;
                if (_movementLookup.HasComponent(entity))
                {
                    var movement = _movementLookup[entity];
                    movementSpeed = math.length(movement.Vel);
                }
                else if (_stealthStatsLookup.HasComponent(entity))
                {
                    // Fallback: approximate from stealth stats penalty
                    var stats = _stealthStatsLookup[entity];
                    movementSpeed = math.abs(stats.MovementPenalty) * 5f;
                }

                // Get environmental context (default values if not available)
                // PHASE 2 TODO: Integrate with actual light/terrain systems:
                //   - Query SignalFieldState for light levels at position
                //   - Query terrain/heightmap systems for terrain type
                //   - Query climate/weather systems for fog/dust modifiers
                // For now, using neutral defaults ensures stealth works but doesn't react to world state.
                // This is acceptable for Phase 1 - stealth checks still function correctly.
                float lightLevel = 0.5f; // Default: neutral lighting (0 = dark, 1 = bright)
                byte terrainType = 1; // Default: urban (0 = open field, 1 = urban, 2 = forest, 3 = fog)

                // Calculate modifiers
                StealthDetectionService.GetEnvironmentalModifiers(
                    lightLevel,
                    terrainType,
                    movementSpeed,
                    out var newModifiers);

                // Preserve equipment modifier (set by other systems)
                newModifiers.EquipmentModifier = modifiers.ValueRO.EquipmentModifier;
                newModifiers.LastUpdateTick = currentTick;

                modifiers.ValueRW = newModifiers;
            }
        }
    }

    /// <summary>
    /// Updates stealth profiles with effective stealth ratings.
    /// Aggregates base rating + modifiers for quick access.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(StealthModifierUpdateSystem))]
    [UpdateBefore(typeof(PerceptionUpdateSystem))]
    public partial struct StealthProfileUpdateSystem : ISystem
    {
        private ComponentLookup<StealthStats> _stealthStatsLookup;
        private ComponentLookup<StealthModifiers> _modifiersLookup;
        private ComponentLookup<MovementState> _movementLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SimulationFeatureFlags>();

            _stealthStatsLookup = state.GetComponentLookup<StealthStats>(true);
            _modifiersLookup = state.GetComponentLookup<StealthModifiers>(true);
            _movementLookup = state.GetComponentLookup<MovementState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.PerceptionEnabled) == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _stealthStatsLookup.Update(ref state);
            _modifiersLookup.Update(ref state);
            _movementLookup.Update(ref state);

            foreach (var (profile, entity) in SystemAPI.Query<RefRW<StealthProfile>>()
                .WithEntityAccess())
            {
                if (!_stealthStatsLookup.HasComponent(entity))
                {
                    continue;
                }

                var stats = _stealthStatsLookup[entity];
                var modifiers = _modifiersLookup.HasComponent(entity)
                    ? _modifiersLookup[entity]
                    : StealthModifiers.Default;

                // Convert VisibilityState to StealthLevel
                StealthLevel level = (StealthLevel)stats.CurrentState;

                // Get movement speed from MovementState if available
                float movementSpeed = 0f;
                if (_movementLookup.HasComponent(entity))
                {
                    movementSpeed = math.length(_movementLookup[entity].Vel);
                }
                else
                {
                    // Fallback: approximate from stealth stats penalty
                    movementSpeed = math.abs(stats.MovementPenalty) * 5f;
                }

                // Calculate effective stealth
                // PHASE 2 TODO: Replace hard-coded light/terrain values with actual environmental queries
                // See StealthModifierUpdateSystem for integration points when light/terrain systems are available
                float effectiveStealth = StealthDetectionService.CalculateEffectiveStealth(
                    stats.BaseStealthRating,
                    level,
                    modifiers,
                    movementSpeed,
                    0.5f, // Default light level (neutral) - Phase 2: query actual light at position
                    0f);  // Default terrain bonus - Phase 2: query actual terrain type at position

                profile.ValueRW.Level = level;
                profile.ValueRW.BaseRating = stats.BaseStealthRating;
                profile.ValueRW.EffectiveRating = effectiveStealth;
                profile.ValueRW.LastUpdateTick = timeState.Tick;
            }
        }
    }
}

