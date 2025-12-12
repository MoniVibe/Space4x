using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using PureDOTS.Systems.Time;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Features.Time
{
    /// <summary>
    /// Space4x-specific adapter system that consumes shared orbit/time-of-day data.
    /// Reads TimeOfDayState and SunlightFactor from planets and applies Space4x-specific effects.
    /// This is a thin adapter layer - all core logic is in PureDOTS shared systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(TimeOfDaySystem))]
    public partial struct Space4XTimeOfDayAdapterSystem : ISystem
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

            // Read orbit/time-of-day data from planets
            // For now, just log phase changes for debugging
            // In the future, this could:
            // - Update URP lighting based on SunlightFactor
            // - Rotate planet mesh based on OrbitalPhase
            // - Apply Space4x-specific effects (mining efficiency, visibility, etc.)

            foreach (var (timeOfDay, sunlight) in SystemAPI.Query<RefRO<TimeOfDayState>, RefRO<SunlightFactor>>())
            {
                var phase = timeOfDay.ValueRO.Phase;
                var previousPhase = timeOfDay.ValueRO.PreviousPhase;

                // Detect phase transitions (for future event handling)
                if (phase != previousPhase)
                {
                    // Phase changed - could raise Space4x-specific events here
                    // For now, just available for future use
                }

                // Sunlight factor is available for Space4x systems to consume
                // Example: Update URP directional light intensity based on sunlight.Sunlight
                // Example: Apply mining efficiency modifiers based on phase
            }
        }
    }
}

