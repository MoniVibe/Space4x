using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Components.Orbital;
using PureDOTS.Runtime.Core;
using Space4X.Orbitals;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Space4X.Systems
{
    /// <summary>
    /// Carrier-specific 6-DoF motion system.
    /// Consumes SixDoFState, applies carrier-specific constraints.
    /// Integrates with existing mining/hauling systems.
    /// Uses Local6DoFSystem (60 Hz) for player-controlled carriers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Orbital.AngularVelocityIntegrationSystem))]
    public partial struct Carrier6DoFMotionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SixDoFState>();
            state.RequireForUpdate<TickTimeState>();
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

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            float deltaTime = tickTimeState.FixedDeltaTime;

            // Process carriers with 6-DoF motion
            var job = new ApplyCarrierConstraintsJob
            {
                DeltaTime = deltaTime
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ApplyCarrierConstraintsJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(
                ref SixDoFState sixDoF,
                in OrbitalObjectTag orbitalTag)
            {
                // Carrier-specific constraints:
                // 1. Limit angular velocity for stability
                // 2. Apply damping to prevent excessive rotation
                // 3. Integrate with mining/hauling systems

                // Angular velocity damping
                const float angularDamping = 0.95f;
                sixDoF.AngularVelocity *= angularDamping;

                // Limit angular velocity magnitude
                float maxAngularVelocity = 1.0f; // rad/s
                float angularVelMagnitude = math.length(sixDoF.AngularVelocity);
                if (angularVelMagnitude > maxAngularVelocity)
                {
                    sixDoF.AngularVelocity = math.normalize(sixDoF.AngularVelocity) * maxAngularVelocity;
                }

                // Linear velocity damping (atmospheric drag simulation)
                const float linearDamping = 0.99f;
                sixDoF.LinearVelocity *= linearDamping;
            }
        }
    }
}

