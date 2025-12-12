using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using PureDOTS.Systems;
using PureDOTS.Systems.Space;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Features.Planets
{
    /// <summary>
    /// Space4x-specific planet system.
    /// Handles Stellaris-like planet logic (colonization hooks, terraforming, etc.).
    /// This is a thin adapter layer - all core logic is in PureDOTS shared systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    // Remove invalid UpdateAfter targeting PlanetAppealSystem (runs in EnvironmentSystemGroup).
    [UpdateAfter(typeof(SpeciesPreferenceMatchingSystem))]
    public partial struct Space4XPlanetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Skip if paused or rewinding
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Space4x-specific planet logic:
            // - Colonization eligibility checks (based on compatibility)
            // - Terraforming progress tracking (future)
            // - Planet-specific effects on mining/economy (future)
            // - UI updates for planet info panels (future)

            // For now, this is a stub that can be extended with Space4x-specific logic
            foreach (var (planetAppeal, compatibility) in SystemAPI.Query<
                RefRO<PlanetAppeal>,
                DynamicBuffer<PlanetCompatibility>>())
            {
                // Space4x can read appeal and compatibility scores here
                // and apply game-specific logic (e.g., colonization eligibility)
            }
        }
    }
}

