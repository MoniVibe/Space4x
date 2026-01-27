using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Miracles;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Copies miracle charge state into presentation-facing display data for HUD/UI systems.
    /// Runs in presentation group so UI can read charge progress without accessing runtime state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct MiracleChargeDisplaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiracleChargeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (chargeState, displayRef) in SystemAPI
                         .Query<RefRO<MiracleChargeState>, RefRW<MiracleChargeDisplayData>>())
            {
                ref readonly var charge = ref chargeState.ValueRO;
                ref var display = ref displayRef.ValueRW;

                display.ChargePercent = charge.Charge01 * 100f;
                display.CurrentTier = charge.TierIndex;
                display.HoldTimeSeconds = charge.HeldTime;
                display.IsCharging = charge.IsCharging;
            }
        }
    }
}

