using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Policies
{
    /// <summary>
    /// Calculates economic stress (tax burden, price spikes, unemployment) and emits unrest events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct UnrestSignalSystem : ISystem
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

            // Calculate unrest signals
            foreach (var (unrest, entity) in SystemAPI.Query<RefRW<UnrestSignal>>().WithEntityAccess())
            {
                // Calculate economic stress factors
                // Tax burden, price spikes, unemployment
                // Emit unrest events if thresholds exceeded
                unrest.ValueRW.TaxBurden = 0f; // Placeholder
                unrest.ValueRW.PriceSpike = 0f; // Placeholder
                unrest.ValueRW.Unemployment = 0f; // Placeholder
            }
        }
    }

    /// <summary>
    /// Unrest signal component.
    /// Tracks economic stress factors.
    /// </summary>
    public struct UnrestSignal : IComponentData
    {
        public Entity TargetEntity;
        public float TaxBurden;
        public float PriceSpike;
        public float Unemployment;
        public float TotalStress;
    }
}

