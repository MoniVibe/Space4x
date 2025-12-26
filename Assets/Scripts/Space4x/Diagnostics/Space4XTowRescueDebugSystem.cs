using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Swarms;
using Space4X.Runtime;
using Space4X.Runtime.Breakables;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Diagnostics
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct Space4XTowRescueDebugSystem : ISystem
    {
        private const uint LogIntervalTicks = 120;
        private static uint s_nextLogTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XSwarmDemoState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var tick = timeState.Tick;
            if (tick < s_nextLogTick)
            {
                return;
            }

            s_nextLogTick = tick + LogIntervalTicks;

            foreach (var (demoState, entity) in SystemAPI.Query<RefRO<Space4XSwarmDemoState>>()
                         .WithEntityAccess())
            {
                var phase = demoState.ValueRO.Phase;
                var hasRescue = state.EntityManager.HasComponent<Space4XTowRescueRequest>(entity);
                Space4XShipCapabilityState capability = default;
                var hasCapability = state.EntityManager.HasComponent<Space4XShipCapabilityState>(entity);
                if (hasCapability)
                {
                    capability = state.EntityManager.GetComponentData<Space4XShipCapabilityState>(entity);
                }

                Debug.Log($"[Space4XSwarmDemo] tick={tick} phase={phase} rescue={(hasRescue ? "yes" : "no")} " +
                          $"alive={(hasCapability ? capability.IsAlive : (byte)0)} mobile={(hasCapability ? capability.IsMobile : (byte)0)} " +
                          $"combat={(hasCapability ? capability.IsCombatCapable : (byte)0)} ftl={(hasCapability ? capability.IsFtlCapable : (byte)0)} " +
                          $"flags={(hasCapability ? capability.ProvidesFlags.ToString() : "none")}");
            }
        }
    }
}
