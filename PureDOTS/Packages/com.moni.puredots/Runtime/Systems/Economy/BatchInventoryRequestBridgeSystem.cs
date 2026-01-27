using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Economy
{
    /// <summary>
    /// Mirrors villager withdraw requests into batch inventory consumption for storehouses that track batch data.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateBefore(typeof(StorehouseInventorySystem))]
    public partial struct BatchInventoryRequestBridgeSystem : ISystem
    {
        private BufferLookup<BatchConsumptionRequest> _batchRequestLookup;
        private ComponentLookup<BatchInventory> _batchInventoryLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BatchInventory>();
            state.RequireForUpdate<TimeState>();
            _batchRequestLookup = state.GetBufferLookup<BatchConsumptionRequest>(false);
            _batchInventoryLookup = state.GetComponentLookup<BatchInventory>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused || (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record))
            {
                return;
            }

            _batchRequestLookup.Update(ref state);
            _batchInventoryLookup.Update(ref state);

            foreach (var (requests, aiState) in SystemAPI.Query<DynamicBuffer<VillagerWithdrawRequest>, RefRO<VillagerAIState>>())
            {
                if (requests.Length == 0)
                {
                    continue;
                }

                var target = aiState.ValueRO.TargetEntity;
                if (target == Entity.Null || !_batchInventoryLookup.HasComponent(target))
                {
                    continue;
                }

                if (!_batchRequestLookup.HasBuffer(target))
                {
                    state.EntityManager.AddBuffer<BatchConsumptionRequest>(target);
                }

                var batchRequests = _batchRequestLookup[target];

                for (int i = 0; i < requests.Length; i++)
                {
                    var req = requests[i];
                    if (req.Amount <= 0f)
                    {
                        continue;
                    }

                    batchRequests.Add(new BatchConsumptionRequest
                    {
                        ResourceId = req.ResourceTypeId,
                        RequestedUnits = math.max(0f, req.Amount)
                    });
                }
            }
        }
    }
}
