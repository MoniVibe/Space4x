using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Transport;
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
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
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
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
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
        private ComponentLookup<StorehouseLedgerSettings> _ledgerSettingsLookup;
        private BufferLookup<StorehouseLedgerEvent> _ledgerEventLookup;
        private BufferLookup<CommsOutboxEntry> _storehouseOutboxLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _storehouseLookup = state.GetComponentLookup<StorehouseConfig>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _storeInventoryLookup = state.GetComponentLookup<StorehouseInventory>(false);
            _capacityLookup = state.GetBufferLookup<StorehouseCapacityElement>(true);
            _storeItemsLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);
            _ledgerSettingsLookup = state.GetComponentLookup<StorehouseLedgerSettings>(true);
            _ledgerEventLookup = state.GetBufferLookup<StorehouseLedgerEvent>(false);
            _storehouseOutboxLookup = state.GetBufferLookup<CommsOutboxEntry>(false);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _storehouseLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _storeInventoryLookup.Update(ref state);
            _capacityLookup.Update(ref state);
            _storeItemsLookup.Update(ref state);
            _ledgerSettingsLookup.Update(ref state);
            _ledgerEventLookup.Update(ref state);
            _storehouseOutboxLookup.Update(ref state);

            // Get resource interaction config or use defaults
            var config = SystemAPI.HasSingleton<ResourceInteractionConfig>()
                ? SystemAPI.GetSingleton<ResourceInteractionConfig>()
                : ResourceInteractionConfig.CreateDefaults();

            // Get resource catalog for type ID to index conversion
            if (!SystemAPI.HasSingleton<ResourceTypeIndex>())
            {
                return;
            }
            var resourceCatalog = SystemAPI.GetSingleton<ResourceTypeIndex>();
            var catalogRef = resourceCatalog.Catalog;

            var withdrawDistance = config.WithdrawDistance;

            foreach (var (requests, inventory, aiState, transform, flags, entity) in
                     SystemAPI.Query<DynamicBuffer<VillagerWithdrawRequest>, DynamicBuffer<VillagerInventoryItem>, VillagerAIState, LocalTransform, VillagerFlags>()
                         .WithNone<PlaybackGuardTag>()
                         .WithEntityAccess())
            {
                var requestsBuffer = requests;
                var inventoryBuffer = inventory;
                
                // Skip dead villagers
                if (flags.IsDead)
                {
                    continue;
                }
                
                if (requestsBuffer.Length == 0)
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
                if (math.distance(transform.Position, storehouseTransform.Position) > withdrawDistance)
                {
                    continue;
                }

                var hasInventory = _storeInventoryLookup.HasComponent(target);
                var storeInventory = hasInventory ? _storeInventoryLookup[target] : default;
                var storeItems = _storeItemsLookup[target];
                var hasLedger = _ledgerSettingsLookup.HasComponent(target) && _ledgerEventLookup.HasBuffer(target);
                var ledgerSettings = hasLedger ? _ledgerSettingsLookup[target] : StorehouseLedgerSettings.Default;

                for (var r = requestsBuffer.Length - 1; r >= 0; r--)
                {
                    var request = requestsBuffer[r];
                    if (request.Amount <= 0f)
                    {
                        requestsBuffer.RemoveAt(r);
                        continue;
                    }

                    // Convert ResourceTypeId to ResourceTypeIndex
                    var resourceTypeIndex = catalogRef.Value.LookupIndex(request.ResourceTypeId);
                    if (resourceTypeIndex < 0 || resourceTypeIndex > ushort.MaxValue)
                    {
                        continue; // Invalid resource type
                    }
                    var ushortTypeIndex = (ushort)resourceTypeIndex;

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
                    for (var i = 0; i < inventoryBuffer.Length; i++)
                    {
                        if (inventoryBuffer[i].ResourceTypeIndex == ushortTypeIndex)
                        {
                            inventoryIndex = i;
                            break;
                        }
                    }

                    if (inventoryIndex >= 0)
                    {
                        var invItem = inventoryBuffer[inventoryIndex];
                        var capacity = invItem.MaxCarryCapacity > 0f ? invItem.MaxCarryCapacity : config.DefaultMaxCarryCapacity;
                        var capacityRemaining = math.max(0f, capacity - invItem.Amount);
                        var taken = math.min(toWithdraw, capacityRemaining);
                        if (taken > 0f)
                        {
                            invItem.Amount += taken;
                            inventoryBuffer[inventoryIndex] = invItem;
                            storeItem.Amount -= taken;
                            request.Amount -= taken;
                            if (hasLedger)
                            {
                                RecordLedgerEvent(target, entity, request.TargetStorehouse, ushortTypeIndex, taken, timeState.Tick, ledgerSettings);
                            }
                            if (hasInventory)
                            {
                                storeInventory.TotalStored -= taken;
                            }
                        }
                    }
                    else
                    {
                        var taken = math.min(toWithdraw, config.DefaultMaxCarryCapacity);
                        if (taken > 0f)
                        {
                            inventoryBuffer.Add(new VillagerInventoryItem
                            {
                                ResourceTypeIndex = ushortTypeIndex,
                                Amount = taken,
                                MaxCarryCapacity = config.DefaultMaxCarryCapacity
                            });
                            storeItem.Amount -= taken;
                            request.Amount -= taken;
                            if (hasLedger)
                            {
                                RecordLedgerEvent(target, entity, request.TargetStorehouse, ushortTypeIndex, taken, timeState.Tick, ledgerSettings);
                            }
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
                        requestsBuffer.RemoveAt(r);
                    }
                    else
                    {
                        requestsBuffer[r] = request;
                    }
                }

                if (hasInventory)
                {
                    _storeInventoryLookup[target] = storeInventory;
                }
            }
        }

        private void RecordLedgerEvent(
            Entity storehouse,
            Entity villager,
            Entity destination,
            ushort resourceTypeIndex,
            float amount,
            uint tick,
            in StorehouseLedgerSettings settings)
        {
            if (amount < settings.EventThresholdUnits ||
                !_ledgerEventLookup.HasBuffer(storehouse))
            {
                return;
            }

            var ledger = _ledgerEventLookup[storehouse];
            ledger.Add(new StorehouseLedgerEvent
            {
                Actor = villager,
                Destination = destination,
                ResourceTypeIndex = resourceTypeIndex,
                Amount = amount,
                Tick = tick
            });

            if (settings.EmitComms == 0 || !_storehouseOutboxLookup.HasBuffer(storehouse))
            {
                return;
            }

            var outbox = _storehouseOutboxLookup[storehouse];
            FixedString32Bytes payload = default;
            payload.Append('s');
            payload.Append('t');
            payload.Append('o');
            payload.Append('r');
            payload.Append('e');
            payload.Append('.');
            payload.Append('a');
            payload.Append('c');
            payload.Append('k');
            payload.Append('.');
            payload.Append((int)resourceTypeIndex);

            outbox.Add(new CommsOutboxEntry
            {
                Token = 0,
                InterruptType = InterruptType.CommsAckReceived,
                Priority = InterruptPriority.Low,
                PayloadId = payload,
                TransportMaskPreferred = PerceptionChannel.Hearing,
                Strength01 = 0.6f,
                Clarity01 = 0.95f,
                DeceptionStrength01 = 0f,
                Secrecy01 = 0f,
                TtlTicks = 10,
                IntendedReceiver = settings.NotifyTarget,
                Flags = settings.NotifyTarget == Entity.Null ? CommsMessageFlags.IsBroadcast : CommsMessageFlags.None,
                FocusCost = 0f,
                MinCohesion01 = 0f,
                RepeatCadenceTicks = 0,
                Attempts = 0,
                MaxAttempts = 0,
                NextEmitTick = 0,
                FirstEmitTick = 0
            });
        }
    }

}
