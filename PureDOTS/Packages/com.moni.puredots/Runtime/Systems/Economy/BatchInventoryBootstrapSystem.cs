using PureDOTS.Runtime.Economy;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Economy
{
    /// <summary>
    /// Ensures batch pricing/config data exists alongside batch inventory.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BatchInventoryBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BatchInventory>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (inventory, entity) in SystemAPI.Query<RefRO<BatchInventory>>().WithNone<BatchPricingState>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new BatchPricingState
                {
                    LastPriceMultiplier = 1f,
                    LastUpdateTick = 0,
                    SmoothedDelta = 0f,
                    LastUnits = inventory.ValueRO.TotalUnits,
                    SmoothedFill = inventory.ValueRO.MaxCapacity > 0f
                        ? math.saturate(inventory.ValueRO.TotalUnits / inventory.ValueRO.MaxCapacity)
                        : 0f
                });
                if (!state.EntityManager.HasComponent<PureDOTS.Runtime.Economy.InventoryFlowState>(entity))
                {
                    ecb.AddComponent(entity, new PureDOTS.Runtime.Economy.InventoryFlowState
                    {
                        LastUnits = inventory.ValueRO.TotalUnits,
                        SmoothedInflow = 0f,
                        SmoothedOutflow = 0f,
                        LastUpdateTick = 0
                    });
                }
            }

            if (!SystemAPI.HasSingleton<BatchPricingConfig>())
            {
                var cfgEntity = ecb.CreateEntity();
                ecb.AddComponent(cfgEntity, BatchPricingConfig.CreateDefault());
            }

            if (!SystemAPI.HasSingleton<PureDOTS.Runtime.Economy.InventoryFlowSettings>())
            {
                var flowEntity = ecb.CreateEntity();
                ecb.AddComponent(flowEntity, PureDOTS.Runtime.Economy.InventoryFlowSettings.CreateDefault());
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
