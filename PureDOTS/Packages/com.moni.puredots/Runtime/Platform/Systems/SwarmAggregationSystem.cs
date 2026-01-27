using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Aggregates nano-swarm and drone wing effects.
    /// Handles swarm splitting/merging. Manages drone wing aggregation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SwarmAggregationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (swarmState, kind, entity) in SystemAPI.Query<RefRW<NanoSwarmState>, RefRO<PlatformKind>>().WithEntityAccess())
            {
                if ((kind.ValueRO.Flags & PlatformFlags.NanoSwarm) == 0)
                {
                    continue;
                }

                UpdateSwarmState(ref swarmState.ValueRW);
            }
        }

        [BurstCompile]
        private static void UpdateSwarmState(ref NanoSwarmState swarmState)
        {
            if (swarmState.ParticleCount <= 0)
            {
                return;
            }

            var density = swarmState.ParticleCount / (math.PI * swarmState.Radius * swarmState.Radius);
            swarmState.Density = density;

            if (swarmState.EnergyReserve <= 0f)
            {
                swarmState.ParticleCount = math.max(0, swarmState.ParticleCount - 1);
            }
        }
    }
}





