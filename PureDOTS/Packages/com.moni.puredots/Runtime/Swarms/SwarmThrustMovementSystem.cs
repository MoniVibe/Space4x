using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Swarms;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Swarms
{
    /// <summary>
    /// Integrates swarm thrust into movement when core engines are offline.
    /// Uses SwarmThrustState.CurrentThrust for acceleration.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(SwarmThrustAggregationSystem))]
    public partial struct SwarmThrustMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<SwarmThrustState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return;
            }

            var deltaTime = tickTime.FixedDeltaTime;

            foreach (var (swarmThrust, transform) in SystemAPI.Query<RefRO<SwarmThrustState>, RefRW<LocalTransform>>())
            {
                var thrust = swarmThrust.ValueRO;

                // Only apply if active
                if (!thrust.Active)
                {
                    continue;
                }

                // Check if engines are offline (simplified check - in real implementation, check engine component)
                // For now, assume engines are offline if SwarmThrustState.Active is true
                // TODO: Add proper engine state check (e.g., HasComponent<EngineState> && !EngineState.Online)

                // Apply swarm thrust acceleration
                if (thrust.CurrentThrust > 0f)
                {
                    float3 thrustVector = math.normalizesafe(thrust.DesiredDirection) * thrust.CurrentThrust;
                    
                    // Simple velocity integration (in real implementation, use PhysicsVelocity or custom velocity component)
                    // For now, directly modify position
                    var currentTransform = transform.ValueRO;
                    float3 newPosition = currentTransform.Position + thrustVector * deltaTime;
                    currentTransform.Position = newPosition;
                    transform.ValueRW = currentTransform;
                }
            }
        }
    }
}

