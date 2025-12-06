using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Components.Orbital;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Space4X.Systems
{
    /// <summary>
    /// Fleet orbital motion system.
    /// Fleet entities inherit orbital motion from parent system.
    /// Uses StellarOrbitSystem (0.01 Hz) for fleet-level motion.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Orbital.SphericalShellUpdateSystem))]
    public partial struct FleetOrbitalSystem : ISystem
    {
        private const float FleetUpdateFrequency = 0.01f; // 0.01 Hz
        private uint _lastUpdateTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleet>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            _lastUpdateTick = 0;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            uint currentTick = tickTimeState.Tick;

            // Check if update is needed based on frequency
            float ticksPerUpdate = 60.0f / FleetUpdateFrequency; // Assuming 60 Hz base
            if (currentTick - _lastUpdateTick < (uint)ticksPerUpdate)
            {
                return;
            }

            _lastUpdateTick = currentTick;

            // Process fleet orbital motion
            var job = new UpdateFleetOrbitalMotionJob
            {
                CurrentTick = currentTick
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct UpdateFleetOrbitalMotionJob : IJobEntity
        {
            public uint CurrentTick;

            public void Execute(
                ref SixDoFState sixDoF,
                in Space4XFleet fleet,
                in ShellMembership shell)
            {
                // Fleet orbital motion inherits from parent stellar system
                // For now, apply simple orbital drift based on shell membership
                // In full implementation, this would:
                // 1. Find parent stellar system
                // 2. Inherit orbital parameters
                // 3. Apply fleet-specific adjustments

                // Simple example: apply mean-field drift based on shell
                float driftSpeed = shell.ShellIndex switch
                {
                    (int)ShellType.Core => 0.1f,
                    (int)ShellType.Inner => 0.05f,
                    (int)ShellType.Outer => 0.01f,
                    _ => 0.05f
                };

                // Apply orbital drift (simplified)
                float3 position = sixDoF.Position;
                float3 toCenter = -math.normalize(position);
                sixDoF.LinearVelocity += toCenter * driftSpeed * 0.01f; // Small adjustment
            }
        }
    }
}

