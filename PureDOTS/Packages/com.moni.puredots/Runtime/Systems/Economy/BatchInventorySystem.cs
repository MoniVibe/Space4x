using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Economy
{
    /// <summary>
    /// Maintains FIFO batch inventories with spoilage and ordered consumption.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BatchInventorySystem : ISystem
    {
        private BufferLookup<BatchConsumptionRequest> _requestsLookup;
        private ComponentLookup<BatchSpoilageSettings> _spoilageLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BatchInventory>();
            state.RequireForUpdate<TimeState>();
            _requestsLookup = state.GetBufferLookup<BatchConsumptionRequest>(isReadOnly: false);
            _spoilageLookup = state.GetComponentLookup<BatchSpoilageSettings>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused || (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record))
            {
                return;
            }

            _requestsLookup.Update(ref state);
            _spoilageLookup.Update(ref state);
            var timeScale = math.max(0f, timeState.CurrentSpeedMultiplier);

            foreach (var (inventoryRW, _, entity) in SystemAPI.Query<RefRW<BatchInventory>, DynamicBuffer<InventoryBatch>>().WithEntityAccess())
            {
                var batches = state.EntityManager.GetBuffer<InventoryBatch>(entity);
                var inventory = inventoryRW.ValueRO;
                var spoilageSettings = _spoilageLookup.HasComponent(entity)
                    ? _spoilageLookup[entity]
                    : BatchSpoilageSettings.CreateDefault();

                float spoiledThisTick = 0f;

                // Spoilage runs FIFO to preserve ordering.
                if ((inventory.Flags & BatchInventoryFlags.DisableSpoilage) == 0)
                {
                    for (int i = 0; i < batches.Length; i++)
                    {
                        var batch = batches[i];
                        if (batch.Units <= 0f)
                        {
                            continue;
                        }

                        var decay = (spoilageSettings.SpoilagePerTick + batch.SpoilagePerTick) * timeScale;
                        if (decay <= 0f)
                        {
                            continue;
                        }

                        var newUnits = batch.Units - decay;
                        if (newUnits <= spoilageSettings.MinRemainderBeforeRemove)
                        {
                            spoiledThisTick += batch.Units;
                            batch.Units = 0f;
                        }
                        else
                        {
                            spoiledThisTick += math.max(0f, decay);
                            batch.Units = newUnits;
                        }

                        batches[i] = batch;
                    }
                }

                if (_requestsLookup.HasBuffer(entity))
                {
                    var requests = _requestsLookup[entity];
                    for (int r = 0; r < requests.Length; r++)
                    {
                        var request = requests[r];
                        if (request.RequestedUnits <= 0f)
                        {
                            continue;
                        }

                        ConsumeFifo(request.ResourceId, request.RequestedUnits, batches, ref inventory);
                    }

                    requests.Clear();
                }

                // Compact empty batches while preserving order.
                for (int i = batches.Length - 1; i >= 0; i--)
                {
                    if (batches[i].Units > 0f)
                    {
                        continue;
                    }
                    batches.RemoveAt(i);
                }

                float totalUnits = 0f;
                for (int i = 0; i < batches.Length; i++)
                {
                    totalUnits += math.max(0f, batches[i].Units);
                }

                inventory.SpoiledUnits += spoiledThisTick;
                inventory.TotalUnits = totalUnits;
                inventory.BatchCount = batches.Length;
                inventory.LastUpdateTick = timeState.Tick;

                inventoryRW.ValueRW = inventory;
            }
        }

        private static void ConsumeFifo(in FixedString64Bytes resourceId, float requestedUnits, DynamicBuffer<InventoryBatch> batches, ref BatchInventory inventory)
        {
            float remaining = requestedUnits;

            for (int i = 0; i < batches.Length && remaining > 0f; i++)
            {
                var batch = batches[i];
                if (batch.Units <= 0f || !batch.ResourceId.Equals(resourceId))
                {
                    continue;
                }

                var take = math.min(batch.Units, remaining);
                batch.Units -= take;
                remaining -= take;
                batches[i] = batch;
            }

            if (remaining > 0f)
            {
                // No-op: callers can inspect inventory to detect shortfall.
            }
        }
    }
}
