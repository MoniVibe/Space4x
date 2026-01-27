using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Economy
{
    /// <summary>
    /// Tracks smoothed inflow/outflow per batch inventory for downstream pricing and trade signals.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BatchInventorySystem))]
    public partial struct InventoryFlowTrackingSystem : ISystem
    {
        private ComponentLookup<InventoryFlowState> _flowLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BatchInventory>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _flowLookup = state.GetComponentLookup<InventoryFlowState>(false);
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

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var settings = SystemAPI.TryGetSingleton<InventoryFlowSettings>(out var flowCfg)
                ? flowCfg
                : InventoryFlowSettings.CreateDefault();
            var smoothing = math.clamp(settings.Smoothing * math.max(1f, timeState.CurrentSpeedMultiplier), 0f, 1f);

            _flowLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (inventory, entity) in SystemAPI.Query<RefRO<BatchInventory>>().WithEntityAccess())
            {
                if (!_flowLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, new InventoryFlowState
                    {
                        LastUnits = inventory.ValueRO.TotalUnits,
                        SmoothedInflow = 0f,
                        SmoothedOutflow = 0f,
                        LastUpdateTick = timeState.Tick
                    });
                    continue;
                }

                var flow = _flowLookup[entity];
                var delta = inventory.ValueRO.TotalUnits - flow.LastUnits;
                var inflow = math.max(0f, delta);
                var outflow = math.max(0f, -delta);

                flow.SmoothedInflow = math.lerp(flow.SmoothedInflow, inflow, smoothing);
                flow.SmoothedOutflow = math.lerp(flow.SmoothedOutflow, outflow, smoothing);
                flow.LastUnits = inventory.ValueRO.TotalUnits;
                flow.LastUpdateTick = timeState.Tick;

                _flowLookup[entity] = flow;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
