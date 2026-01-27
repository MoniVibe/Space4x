using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Policies
{
    /// <summary>
    /// Accrues interest on loans, schedules payments.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct LoanAccrualSystem : ISystem
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
            var tick = tickTimeState.Tick;
            var deltaTime = tickTimeState.FixedDeltaTime;

            foreach (var (loan, entity) in SystemAPI.Query<RefRW<LoanRecord>>().WithEntityAccess())
            {
                // Accrue interest
                var interestThisTick = loan.ValueRO.RemainingPrincipal * loan.ValueRO.InterestRate * deltaTime;
                loan.ValueRW.AccruedInterest += interestThisTick;

                // Check for payment due
                if (tick >= loan.ValueRO.NextPaymentTick)
                {
                    // Schedule payment (handled by LoanDefaultSystem if not paid)
                    loan.ValueRW.NextPaymentTick = tick + 30; // Monthly payments
                }
            }
        }
    }
}

