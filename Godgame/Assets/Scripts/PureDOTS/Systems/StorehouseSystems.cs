using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Aggregates storehouse inventory state each frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(ResourceDepositSystem))]
    public partial struct StorehouseInventorySystem : ISystem
    {
        private EntityQuery _storehouseQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _storehouseQuery = SystemAPI.QueryBuilder()
                .WithAll<StorehouseConfig, StorehouseInventory>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate(_storehouseQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var updateJob = new UpdateStorehouseInventoryJob
            {
                DeltaTime = timeState.FixedDeltaTime,
                CurrentTick = timeState.Tick
            };

            state.Dependency = updateJob.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdateStorehouseInventoryJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;

            public void Execute(
                ref StorehouseInventory inventory,
                ref DynamicBuffer<StorehouseInventoryItem> items,
                ref DynamicBuffer<StorehouseCapacityElement> capacities,
                in StorehouseConfig config)
            {
                var totalStored = 0f;
                for (var i = 0; i < items.Length; i++)
                {
                    totalStored += items[i].Amount;
                }

                var totalCapacity = 0f;
                for (var i = 0; i < capacities.Length; i++)
                {
                    totalCapacity += capacities[i].MaxCapacity;
                }

                inventory.TotalStored = totalStored;
                inventory.TotalCapacity = totalCapacity;
                inventory.ItemTypeCount = items.Length;
                inventory.LastUpdateTick = CurrentTick;

                if (inventory.IsShredding == 0 || config.ShredRate <= 0f)
                {
                    return;
                }

                var shredAmount = config.ShredRate * DeltaTime;
                for (var i = 0; i < items.Length && shredAmount > 0f; i++)
                {
                    var item = items[i];
                    var available = math.max(0f, item.Amount - item.Reserved);
                    if (available <= 0f)
                    {
                        continue;
                    }

                    var toShred = math.min(available, shredAmount);
                    item.Amount -= toShred;
                    shredAmount -= toShred;
                    items[i] = item;

                    if (item.Amount <= 0f)
                    {
                        items.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Placeholder for future queued deposit processing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(StorehouseInventorySystem))]
    public partial struct StorehouseDepositProcessingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }
            // Queue-based deposits to be implemented in later passes.
        }
    }

    /// <summary>
    /// Records storehouse state samples for rewind playback.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(StorehouseInventorySystem))]
    public partial struct StorehouseHistoryRecordingSystem : ISystem
    {
        private uint _lastRecordedTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<HistorySettings>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var historySettings = SystemAPI.GetSingleton<HistorySettings>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var strideTicks = (uint)math.max(1f, 5f / math.max(0.0001f, timeState.FixedDeltaTime));
            if (timeState.Tick % strideTicks != 0 || _lastRecordedTick == timeState.Tick)
            {
                return;
            }

            _lastRecordedTick = timeState.Tick;

            foreach (var (inventory, historyBuffer) in
                     SystemAPI.Query<RefRO<StorehouseInventory>, DynamicBuffer<StorehouseHistorySample>>()
                         .WithAll<RewindableTag>())
            {
                var sample = new StorehouseHistorySample
                {
                    Tick = timeState.Tick,
                    ShredQueueCount = 0,
                    IsShredding = inventory.ValueRO.IsShredding,
                    TotalCapacity = inventory.ValueRO.TotalCapacity,
                    LastDepositTick = inventory.ValueRO.LastUpdateTick
                };

                PruneOldSamples(historyBuffer, timeState.Tick, historySettings.DefaultHorizonSeconds, timeState.FixedDeltaTime);
                historyBuffer.Add(sample);
            }
        }

        private static void PruneOldSamples(
            DynamicBuffer<StorehouseHistorySample> buffer,
            uint currentTick,
            float horizonSeconds,
            float fixedDt)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            var maxHistoryTicks = (uint)math.max(1f, horizonSeconds / math.max(0.0001f, fixedDt));
            var cutoffTick = currentTick > maxHistoryTicks ? currentTick - maxHistoryTicks : 0;

            var firstValidIndex = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Tick >= cutoffTick)
                {
                    firstValidIndex = i;
                    break;
                }
            }

            if (firstValidIndex <= 0)
            {
                return;
            }

            for (var i = 0; i < buffer.Length - firstValidIndex; i++)
            {
                buffer[i] = buffer[i + firstValidIndex];
            }

            buffer.RemoveRange(buffer.Length - firstValidIndex, firstValidIndex);
        }
    }

    /// <summary>
    /// Handles villager withdrawals from storehouses when near their target.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(StorehouseInventorySystem))]
    public partial struct StorehouseWithdrawalProcessingSystem : ISystem
    {
        private ComponentLookup<StorehouseConfig> _storehouseLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<StorehouseInventory> _storeInventoryLookup;
        private BufferLookup<StorehouseCapacityElement> _capacityLookup;
        private BufferLookup<StorehouseInventoryItem> _storeItemsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _storehouseLookup = state.GetComponentLookup<StorehouseConfig>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _storeInventoryLookup = state.GetComponentLookup<StorehouseInventory>(false);
            _capacityLookup = state.GetBufferLookup<StorehouseCapacityElement>(true);
            _storeItemsLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);

            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _storehouseLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _storeInventoryLookup.Update(ref state);
            _capacityLookup.Update(ref state);
            _storeItemsLookup.Update(ref state);

            const float WithdrawDistance = 5f;

            foreach (var tuple in
                     SystemAPI.Query<DynamicBuffer<VillagerWithdrawRequest>, DynamicBuffer<VillagerInventoryItem>, VillagerAIState, LocalTransform>()
                         .WithEntityAccess())
            {
                var requests = tuple.Item1;
                var inventory = tuple.Item2;
                var aiState = tuple.Item3;
                var transform = tuple.Item4;
                if (requests.Length == 0)
                {
                    continue;
                }

                var target = aiState.TargetEntity;
                if (target == Entity.Null ||
                    !_storehouseLookup.HasComponent(target) ||
                    !_transformLookup.HasComponent(target) ||
                    !_storeItemsLookup.HasBuffer(target))
                {
                    continue;
                }

                var storehouseTransform = _transformLookup[target];
                if (math.distance(transform.Position, storehouseTransform.Position) > WithdrawDistance)
                {
                    continue;
                }

                var hasInventory = _storeInventoryLookup.HasComponent(target);
                var storeInventory = hasInventory ? _storeInventoryLookup[target] : default;
                var storeItems = _storeItemsLookup[target];

                for (var r = requests.Length - 1; r >= 0; r--)
                {
                    var request = requests[r];
                    if (request.Amount <= 0f)
                    {
                        requests.RemoveAt(r);
                        continue;
                    }

                    var itemIndex = -1;
                    for (var i = 0; i < storeItems.Length; i++)
                    {
                        if (storeItems[i].ResourceTypeId.Equals(request.ResourceTypeId))
                        {
                            itemIndex = i;
                            break;
                        }
                    }

                    if (itemIndex < 0)
                    {
                        continue;
                    }

                    var storeItem = storeItems[itemIndex];
                    var available = math.max(0f, storeItem.Amount - storeItem.Reserved);
                    if (available <= 0f)
                    {
                        continue;
                    }

                    var toWithdraw = math.min(request.Amount, available);
                    if (toWithdraw <= 0f)
                    {
                        continue;
                    }

                    var inventoryIndex = -1;
                    for (var i = 0; i < inventory.Length; i++)
                    {
                        if (inventory[i].ResourceTypeId.Equals(request.ResourceTypeId))
                        {
                            inventoryIndex = i;
                            break;
                        }
                    }

                    if (inventoryIndex >= 0)
                    {
                        var invItem = inventory[inventoryIndex];
                        var capacity = invItem.MaxCarryCapacity > 0f ? invItem.MaxCarryCapacity : 50f;
                        var capacityRemaining = math.max(0f, capacity - invItem.Amount);
                        var taken = math.min(toWithdraw, capacityRemaining);
                        if (taken > 0f)
                        {
                            invItem.Amount += taken;
                            inventory[inventoryIndex] = invItem;
                            storeItem.Amount -= taken;
                            request.Amount -= taken;
                            if (hasInventory)
                            {
                                storeInventory.TotalStored -= taken;
                            }
                        }
                    }
                    else
                    {
                        var taken = math.min(toWithdraw, 50f);
                        if (taken > 0f)
                        {
                            inventory.Add(new VillagerInventoryItem
                            {
                                ResourceTypeId = request.ResourceTypeId,
                                Amount = taken,
                                MaxCarryCapacity = 50f
                            });
                            storeItem.Amount -= taken;
                            request.Amount -= taken;
                            if (hasInventory)
                            {
                                storeInventory.TotalStored -= taken;
                                storeInventory.ItemTypeCount = storeItems.Length;
                            }
                        }
                    }

                    storeItems[itemIndex] = storeItem;

                    if (request.Amount <= 0f)
                    {
                        requests.RemoveAt(r);
                    }
                    else
                    {
                        requests[r] = request;
                    }
                }

                if (hasInventory)
                {
                    _storeInventoryLookup[target] = storeInventory;
                }
            }
        }
    }

    /// <summary>
    /// Simple utility helpers for external systems to interact with storehouses.
    /// </summary>
    public static class StorehouseAPI
    {
        public static bool TryDeposit(
            EntityManager entityManager,
            Entity storehouseEntity,
            FixedString64Bytes resourceTypeId,
            float amount,
            out float accepted)
        {
            accepted = 0f;

            if (!entityManager.HasComponent<StorehouseInventory>(storehouseEntity) ||
                !entityManager.HasBuffer<StorehouseInventoryItem>(storehouseEntity) ||
                !entityManager.HasBuffer<StorehouseCapacityElement>(storehouseEntity))
            {
                return false;
            }

            var inventory = entityManager.GetComponentData<StorehouseInventory>(storehouseEntity);
            var items = entityManager.GetBuffer<StorehouseInventoryItem>(storehouseEntity);
            var capacities = entityManager.GetBuffer<StorehouseCapacityElement>(storehouseEntity);

            var maxCapacity = 0f;
            for (var i = 0; i < capacities.Length; i++)
            {
                if (capacities[i].ResourceTypeId.Equals(resourceTypeId))
                {
                    maxCapacity = capacities[i].MaxCapacity;
                    break;
                }
            }

            if (maxCapacity <= 0f)
            {
                return false;
            }

            var itemIndex = -1;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].ResourceTypeId.Equals(resourceTypeId))
                {
                    itemIndex = i;
                    break;
                }
            }

            if (itemIndex >= 0)
            {
                var item = items[itemIndex];
                var available = math.max(0f, maxCapacity - item.Amount);
                accepted = math.min(amount, available);
                item.Amount += accepted;
                items[itemIndex] = item;
            }
            else
            {
                accepted = math.min(amount, maxCapacity);
                items.Add(new StorehouseInventoryItem
                {
                    ResourceTypeId = resourceTypeId,
                    Amount = accepted,
                    Reserved = 0f
                });
            }

            inventory.TotalStored += accepted;
            inventory.ItemTypeCount = items.Length;
            entityManager.SetComponentData(storehouseEntity, inventory);

            return accepted > 0f;
        }

        public static bool TryWithdraw(
            EntityManager entityManager,
            Entity storehouseEntity,
            FixedString64Bytes resourceTypeId,
            float amount,
            out float withdrawn)
        {
            withdrawn = 0f;

            if (!entityManager.HasBuffer<StorehouseInventoryItem>(storehouseEntity))
            {
                return false;
            }

            var items = entityManager.GetBuffer<StorehouseInventoryItem>(storehouseEntity);

            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (!item.ResourceTypeId.Equals(resourceTypeId))
                {
                    continue;
                }

                var available = item.Amount - item.Reserved;
                withdrawn = math.min(amount, available);
                if (withdrawn <= 0f)
                {
                    return false;
                }

                item.Amount -= withdrawn;
                items[i] = item;

                if (item.Amount <= 0f)
                {
                    items.RemoveAt(i);
                }

                if (entityManager.HasComponent<StorehouseInventory>(storehouseEntity))
                {
                    var inventory = entityManager.GetComponentData<StorehouseInventory>(storehouseEntity);
                    inventory.TotalStored -= withdrawn;
                    inventory.ItemTypeCount = items.Length;
                    entityManager.SetComponentData(storehouseEntity, inventory);
                }

                return true;
            }

            return false;
        }

        public static bool TryReserve(
            EntityManager entityManager,
            Entity storehouseEntity,
            FixedString64Bytes resourceTypeId,
            float amount,
            out float reserved)
        {
            reserved = 0f;

            if (!entityManager.HasBuffer<StorehouseInventoryItem>(storehouseEntity))
            {
                return false;
            }

            var items = entityManager.GetBuffer<StorehouseInventoryItem>(storehouseEntity);
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (!item.ResourceTypeId.Equals(resourceTypeId))
                {
                    continue;
                }

                var available = item.Amount - item.Reserved;
                reserved = math.min(amount, available);
                if (reserved <= 0f)
                {
                    return false;
                }

                item.Reserved += reserved;
                items[i] = item;
                return true;
            }

            return false;
        }
    }
}
