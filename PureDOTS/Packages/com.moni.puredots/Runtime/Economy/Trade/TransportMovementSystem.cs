using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy.Trade
{
    /// <summary>
    /// Advances transports along routes (distance + speed + terrain modifiers).
    /// Updates TransportProgress component.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TransportMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var deltaTime = tickTimeState.FixedDeltaTime * math.max(0f, tickTimeState.CurrentSpeedMultiplier);

            foreach (var (transport, progress, entity) in SystemAPI.Query<RefRO<TransportEntity>, RefRW<TransportProgress>>().WithEntityAccess())
            {
                var speed = transport.ValueRO.Speed;
                var distanceThisTick = speed * deltaTime;

                progress.ValueRW.DistanceTraveled += distanceThisTick;
                progress.ValueRW.LegProgress = math.min(1f, progress.ValueRW.DistanceTraveled / 100f); // Simplified distance calculation

                if (progress.ValueRO.LegProgress >= 1f)
                {
                    // Arrived at destination
                    // TransportArrivalSystem will handle unloading
                }
            }
        }
    }
}

