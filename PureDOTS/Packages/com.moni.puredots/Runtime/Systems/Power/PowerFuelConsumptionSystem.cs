using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using PureDOTS.Runtime.Resource;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Power
{
    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PowerGenerationSystem))]
    public partial struct PowerFuelConsumptionSystem : ISystem
    {
        private ComponentLookup<StorehouseInventory> _inventoryLookup;
        private BufferLookup<StorehouseInventoryItem> _itemLookup;
        private ComponentLookup<FuelStorageRef> _storageRefLookup;
        private BufferLookup<FuelBlendElement> _blendLookup;
        private ComponentLookup<FuelConsumerState> _fuelStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ResourceTypeIndex>();
            state.RequireForUpdate<FuelConsumer>();

            _inventoryLookup = state.GetComponentLookup<StorehouseInventory>(false);
            _itemLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);
            _storageRefLookup = state.GetComponentLookup<FuelStorageRef>(true);
            _blendLookup = state.GetBufferLookup<FuelBlendElement>(true);
            _fuelStateLookup = state.GetComponentLookup<FuelConsumerState>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ResourceTypeIndex>(out var resourceIndex) ||
                !resourceIndex.Catalog.IsCreated)
            {
                return;
            }

            var deltaTime = math.max(0f, time.FixedDeltaTime);
            if (deltaTime <= 0f)
            {
                return;
            }

            _inventoryLookup.Update(ref state);
            _itemLookup.Update(ref state);
            _storageRefLookup.Update(ref state);
            _blendLookup.Update(ref state);
            _fuelStateLookup.Update(ref state);

            var catalog = resourceIndex.Catalog;
            var tick = time.Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (generator, fuelConsumer, entity) in SystemAPI
                         .Query<RefRO<PowerGenerator>, RefRO<FuelConsumer>>()
                         .WithEntityAccess())
            {
                if (fuelConsumer.ValueRO.DemandMode != FuelDemandMode.GeneratorOutput)
                {
                    continue;
                }

                if (!TryResolveStorage(entity, out var storageEntity, out var inventory, out var items))
                {
                    UpdateFuelState(entity, tick, 0f, 0f, 0f, 1, ref ecb);
                    continue;
                }

                var requestedOutput = PowerCoreMath.CalculateActualOutput(
                    generator.ValueRO.TheoreticalMaxOutput,
                    generator.ValueRO.CurrentOutputPercent,
                    generator.ValueRO.Efficiency,
                    generator.ValueRO.DegradationLevel,
                    out _);

                if (requestedOutput <= 0f)
                {
                    UpdateFuelState(entity, tick, 1f, 0f, 0f, 0, ref ecb);
                    continue;
                }

                var ratio = 1f;
                var requestedUnits = 0f;
                var consumedUnits = 0f;
                var starved = (byte)0;

                if (_blendLookup.HasBuffer(entity) && _blendLookup[entity].Length > 0)
                {
                    ratio = ConsumeBlend(entity, requestedOutput, deltaTime, fuelConsumer.ValueRO.AllowPartial != 0,
                        catalog, ref inventory, items);
                    requestedUnits = ResolveBlendRequest(entity, requestedOutput, deltaTime);
                    consumedUnits = requestedUnits * ratio;
                }
                else
                {
                    requestedUnits = requestedOutput * math.max(0f, fuelConsumer.ValueRO.FuelPerMW) * deltaTime;
                    if (requestedUnits > 0f && fuelConsumer.ValueRO.DefaultFuelId.Length > 0)
                    {
                        ratio = ConsumeSingle(fuelConsumer.ValueRO.DefaultFuelId, requestedUnits,
                            fuelConsumer.ValueRO.AllowPartial != 0, catalog, ref inventory, items, out consumedUnits);
                    }
                }

                if (ratio < 0.999f && requestedUnits > 0f)
                {
                    starved = 1;
                }

                UpdateFuelState(entity, tick, ratio, requestedUnits, consumedUnits, starved, ref ecb);
                _inventoryLookup[storageEntity] = inventory;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private bool TryResolveStorage(
            Entity entity,
            out Entity storageEntity,
            out StorehouseInventory inventory,
            out DynamicBuffer<StorehouseInventoryItem> items)
        {
            storageEntity = entity;
            if (_storageRefLookup.HasComponent(entity))
            {
                var storageRef = _storageRefLookup[entity];
                if (storageRef.Storage != Entity.Null)
                {
                    storageEntity = storageRef.Storage;
                }
            }

            inventory = default;
            items = default;

            if (!_inventoryLookup.HasComponent(storageEntity) || !_itemLookup.HasBuffer(storageEntity))
            {
                return false;
            }

            inventory = _inventoryLookup[storageEntity];
            items = _itemLookup[storageEntity];
            return true;
        }

        private float ConsumeBlend(
            Entity entity,
            float requestedOutput,
            float deltaTime,
            bool allowPartial,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items)
        {
            var blend = _blendLookup[entity];
            var ratio = 1f;

            for (int i = 0; i < blend.Length; i++)
            {
                var entry = blend[i];
                if (entry.ResourceId.Length == 0)
                {
                    continue;
                }

                if (!TryResolveResourceTypeIndex(entry.ResourceId, catalog, out _))
                {
                    return 0f;
                }

                var required = requestedOutput * math.max(0f, entry.FuelPerMW) * deltaTime;
                if (required <= 0f)
                {
                    continue;
                }

                var available = GetAvailable(items, entry.ResourceId);
                var entryRatio = available / required;

                if (!allowPartial && entryRatio + 1e-3f < 1f)
                {
                    return 0f;
                }

                ratio = math.min(ratio, entryRatio);
            }

            ratio = math.clamp(ratio, 0f, 1f);
            if (ratio <= 0f)
            {
                return 0f;
            }

            for (int i = 0; i < blend.Length; i++)
            {
                var entry = blend[i];
                if (entry.ResourceId.Length == 0)
                {
                    continue;
                }

                var required = requestedOutput * math.max(0f, entry.FuelPerMW) * deltaTime;
                if (required <= 0f)
                {
                    continue;
                }

                var consume = required * ratio;
                if (!TryResolveResourceTypeIndex(entry.ResourceId, catalog, out var index))
                {
                    continue;
                }

                StorehouseMutationService.TryConsumeUnreserved(index, consume, catalog, ref inventory, items);
            }

            return ratio;
        }

        private float ResolveBlendRequest(Entity entity, float requestedOutput, float deltaTime)
        {
            var blend = _blendLookup[entity];
            var request = 0f;
            for (int i = 0; i < blend.Length; i++)
            {
                var entry = blend[i];
                if (entry.ResourceId.Length == 0)
                {
                    continue;
                }

                request += requestedOutput * math.max(0f, entry.FuelPerMW) * deltaTime;
            }

            return request;
        }

        private float ConsumeSingle(
            FixedString64Bytes resourceId,
            float requestedUnits,
            bool allowPartial,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items,
            out float consumed)
        {
            consumed = 0f;
            if (requestedUnits <= 0f || resourceId.Length == 0)
            {
                return 1f;
            }

            var available = GetAvailable(items, resourceId);
            var ratio = allowPartial
                ? math.clamp(available / requestedUnits, 0f, 1f)
                : (available + 1e-3f >= requestedUnits ? 1f : 0f);

            if (ratio <= 0f)
            {
                return 0f;
            }

            if (!TryResolveResourceTypeIndex(resourceId, catalog, out var index))
            {
                return 0f;
            }

            consumed = requestedUnits * ratio;
            StorehouseMutationService.TryConsumeUnreserved(index, consumed, catalog, ref inventory, items);
            return ratio;
        }

        private static float GetAvailable(DynamicBuffer<StorehouseInventoryItem> items, FixedString64Bytes resourceId)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (!item.ResourceTypeId.Equals(resourceId))
                {
                    continue;
                }

                return math.max(0f, item.Amount - item.Reserved);
            }

            return 0f;
        }

        private static bool TryResolveResourceTypeIndex(
            FixedString64Bytes resourceId,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            out ushort index)
        {
            var resolved = catalog.Value.LookupIndex(resourceId);
            if (resolved < 0)
            {
                index = ushort.MaxValue;
                return false;
            }

            index = (ushort)resolved;
            return true;
        }

        private void UpdateFuelState(
            Entity entity,
            uint tick,
            float ratio,
            float requestedUnits,
            float consumedUnits,
            byte starved,
            ref EntityCommandBuffer ecb)
        {
            var state = new FuelConsumerState
            {
                FuelRatio = math.clamp(ratio, 0f, 1f),
                RequestedUnits = math.max(0f, requestedUnits),
                ConsumedUnits = math.max(0f, consumedUnits),
                Starved = starved,
                LastTick = tick
            };

            if (_fuelStateLookup.HasComponent(entity))
            {
                _fuelStateLookup[entity] = state;
            }
            else
            {
                ecb.AddComponent(entity, state);
            }
        }
    }
}
