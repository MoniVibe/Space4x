using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Time
{
    /// <summary>
    /// Advances orbital phase for planets orbiting stars.
    /// Burst-compiled system that updates OrbitState based on TimeState.
    /// Runs in EnvironmentSystemGroup to provide orbital data for time-of-day calculations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    public partial struct OrbitAdvanceSystem : ISystem
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
            if (timeState.IsPaused)
            {
                return;
            }

            // Skip if paused or rewinding (orbit state should come from history during rewind)
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = timeState.DeltaTime;
            var currentTick = timeState.Tick;

            // Process all planets with orbit parameters
            foreach (var (orbitParams, orbitState) in SystemAPI.Query<RefRO<OrbitParameters>, RefRW<OrbitState>>())
            {
                var period = orbitParams.ValueRO.OrbitalPeriodSeconds;

                // Guard against invalid period
                if (period <= 0f)
                {
                    continue;
                }

                // Advance orbital phase: delta = time elapsed / period
                var phaseDelta = deltaTime / period;
                var newPhase = orbitState.ValueRO.OrbitalPhase + phaseDelta;

                // Wrap phase to [0, 1)
                newPhase = math.frac(newPhase);

                // Update orbit state
                orbitState.ValueRW.OrbitalPhase = newPhase;
                orbitState.ValueRW.LastUpdateTick = currentTick;
            }
        }
    }
}

