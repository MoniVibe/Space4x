using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(AIVirtualSensorSystem))]
    [UpdateBefore(typeof(AIUtilityScoringSystem))]
    public partial struct AIResourceContextScoringSystem : ISystem
    {
        private ComponentLookup<ResourceTypeId> _resourceTypeLookup;
        private BufferLookup<StorehouseInventoryItem> _storehouseInventoryLookup;
        private ComponentLookup<VillagerNeedState> _needStateLookup;
        private ComponentLookup<VillagerNeeds> _needsLookup;
        private ComponentLookup<VillagerInventoryRef> _inventoryRefLookup;
        private BufferLookup<VillagerInventoryItem> _inventoryItemLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MindCadenceSettings>();

            _resourceTypeLookup = state.GetComponentLookup<ResourceTypeId>(true);
            _storehouseInventoryLookup = state.GetBufferLookup<StorehouseInventoryItem>(true);
            _needStateLookup = state.GetComponentLookup<VillagerNeedState>(true);
            _needsLookup = state.GetComponentLookup<VillagerNeeds>(true);
            _inventoryRefLookup = state.GetComponentLookup<VillagerInventoryRef>(true);
            _inventoryItemLookup = state.GetBufferLookup<VillagerInventoryItem>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var cadenceSettings = SystemAPI.GetSingleton<MindCadenceSettings>();
            if (!CadenceGate.ShouldRun(timeState.Tick, cadenceSettings.SensorCadenceTicks))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _resourceTypeLookup.Update(ref state);
            _storehouseInventoryLookup.Update(ref state);
            _needStateLookup.Update(ref state);
            _needsLookup.Update(ref state);
            _inventoryRefLookup.Update(ref state);
            _inventoryItemLookup.Update(ref state);

            var hasCatalog = SystemAPI.TryGetSingletonBuffer<ResourceValueEntry>(out var catalogBuffer, true);
            var hasResourceIndex = SystemAPI.TryGetSingleton<ResourceTypeIndex>(out var resourceIndex);
            var resourceCatalog = hasResourceIndex ? resourceIndex.Catalog : default;

            foreach (var (readingsBuffer, entity) in SystemAPI.Query<DynamicBuffer<AISensorReading>>().WithEntityAccess())
            {
                var readings = readingsBuffer;
                if (readings.Length == 0)
                {
                    continue;
                }

                var hungerUrgency = ResolveHungerUrgency(entity, _needStateLookup, _needsLookup);
                var hasInventory = TryResolveInventory(entity, _inventoryRefLookup, _inventoryItemLookup, out var inventoryBuffer);

                for (int i = 0; i < readings.Length; i++)
                {
                    var reading = readings[i];
                    if (reading.Category != AISensorCategory.ResourceNode &&
                        reading.Category != AISensorCategory.Storehouse)
                    {
                        continue;
                    }

                    if (reading.Target == Entity.Null)
                    {
                        continue;
                    }

                    if (reading.Category == AISensorCategory.ResourceNode)
                    {
                        if (!_resourceTypeLookup.HasComponent(reading.Target))
                        {
                            continue;
                        }

                        var resourceTypeId = _resourceTypeLookup[reading.Target].Value;
                        var priority = ComputeResourcePriority(resourceTypeId, hungerUrgency, hasCatalog, catalogBuffer, hasInventory, inventoryBuffer, resourceCatalog);
                        reading.NormalizedScore = math.saturate(reading.NormalizedScore * priority);
                        readings[i] = reading;
                        continue;
                    }

                    if (reading.Category == AISensorCategory.Storehouse)
                    {
                        if (!_storehouseInventoryLookup.HasBuffer(reading.Target))
                        {
                            continue;
                        }

                        var storehouseItems = _storehouseInventoryLookup[reading.Target];
                        if (!TryComputeStorehousePriority(storehouseItems, hungerUrgency, hasCatalog, catalogBuffer, hasInventory, inventoryBuffer, resourceCatalog, out var priority))
                        {
                            continue;
                        }

                        reading.NormalizedScore = math.saturate(reading.NormalizedScore * priority);
                        readings[i] = reading;
                    }
                }
            }
        }

        private static float ResolveHungerUrgency(
            Entity entity,
            ComponentLookup<VillagerNeedState> needStateLookup,
            ComponentLookup<VillagerNeeds> needsLookup)
        {
            if (needStateLookup.HasComponent(entity))
            {
                return math.saturate(needStateLookup[entity].HungerUrgency);
            }

            if (needsLookup.HasComponent(entity))
            {
                var needs = needsLookup[entity];
                return 1f - math.saturate(needs.HungerFloat / 100f);
            }

            return 0f;
        }

        private static bool TryResolveInventory(
            Entity entity,
            ComponentLookup<VillagerInventoryRef> inventoryRefLookup,
            BufferLookup<VillagerInventoryItem> inventoryItemLookup,
            out DynamicBuffer<VillagerInventoryItem> inventoryBuffer)
        {
            if (!inventoryRefLookup.HasComponent(entity))
            {
                inventoryBuffer = default;
                return false;
            }

            var inventoryRef = inventoryRefLookup[entity];
            if (inventoryRef.CompanionEntity == Entity.Null)
            {
                inventoryBuffer = default;
                return false;
            }

            if (!inventoryItemLookup.HasBuffer(inventoryRef.CompanionEntity))
            {
                inventoryBuffer = default;
                return false;
            }

            inventoryBuffer = inventoryItemLookup[inventoryRef.CompanionEntity];
            return true;
        }

        private static float ComputeResourcePriority(
            in FixedString64Bytes resourceTypeId,
            float hungerUrgency,
            bool hasCatalog,
            DynamicBuffer<ResourceValueEntry> catalogBuffer,
            bool hasInventory,
            DynamicBuffer<VillagerInventoryItem> inventoryBuffer,
            BlobAssetReference<ResourceTypeIndexBlob> resourceCatalog)
        {
            var baseValue = hasCatalog ? LookupBaseValue(resourceTypeId, catalogBuffer) : 1f;
            baseValue = math.saturate(baseValue);

            var needWeight = IsFoodResource(resourceTypeId) ? hungerUrgency : 0f;
            var priority = math.max(needWeight, baseValue);

            if (hasInventory)
            {
                var inventoryPenalty = ComputeInventoryPenalty(resourceTypeId, inventoryBuffer, resourceCatalog);
                priority *= (1f - inventoryPenalty);
            }

            return math.saturate(priority);
        }

        private static bool TryComputeStorehousePriority(
            DynamicBuffer<StorehouseInventoryItem> items,
            float hungerUrgency,
            bool hasCatalog,
            DynamicBuffer<ResourceValueEntry> catalogBuffer,
            bool hasInventory,
            DynamicBuffer<VillagerInventoryItem> inventoryBuffer,
            BlobAssetReference<ResourceTypeIndexBlob> resourceCatalog,
            out float priority)
        {
            priority = 0f;

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item.Amount <= 0f || item.ResourceTypeId.Length == 0)
                {
                    continue;
                }

                var candidate = ComputeResourcePriority(item.ResourceTypeId, hungerUrgency, hasCatalog, catalogBuffer, hasInventory, inventoryBuffer, resourceCatalog);
                if (candidate > priority)
                {
                    priority = candidate;
                }
            }

            return priority > 0f;
        }

        private static float LookupBaseValue(in FixedString64Bytes resourceTypeId, DynamicBuffer<ResourceValueEntry> catalogBuffer)
        {
            for (int i = 0; i < catalogBuffer.Length; i++)
            {
                if (catalogBuffer[i].ResourceTypeId.Equals(resourceTypeId))
                {
                    return catalogBuffer[i].BaseValue;
                }
            }

            return 1f;
        }

        private static float ComputeInventoryPenalty(
            in FixedString64Bytes resourceTypeId,
            DynamicBuffer<VillagerInventoryItem> inventoryBuffer,
            BlobAssetReference<ResourceTypeIndexBlob> resourceCatalog)
        {
            if (!resourceCatalog.IsCreated)
            {
                return 0f;
            }

            var resourceIndex = resourceCatalog.Value.LookupIndex(resourceTypeId);
            if (resourceIndex < 0)
            {
                return 0f;
            }

            for (int i = 0; i < inventoryBuffer.Length; i++)
            {
                var item = inventoryBuffer[i];
                if (item.ResourceTypeIndex != resourceIndex)
                {
                    continue;
                }

                if (item.MaxCarryCapacity <= 0f)
                {
                    return 0f;
                }

                return math.saturate(item.Amount / math.max(1e-3f, item.MaxCarryCapacity));
            }

            return 0f;
        }

        private static bool IsFoodResource(in FixedString64Bytes resourceTypeId)
        {
            if (resourceTypeId.Length == 4 &&
                resourceTypeId[0] == (byte)'f' &&
                resourceTypeId[1] == (byte)'o' &&
                resourceTypeId[2] == (byte)'o' &&
                resourceTypeId[3] == (byte)'d')
            {
                return true;
            }

            if (resourceTypeId.Length == 14 &&
                resourceTypeId[0] == (byte)'p' &&
                resourceTypeId[1] == (byte)'r' &&
                resourceTypeId[2] == (byte)'e' &&
                resourceTypeId[3] == (byte)'s' &&
                resourceTypeId[4] == (byte)'e' &&
                resourceTypeId[5] == (byte)'r' &&
                resourceTypeId[6] == (byte)'v' &&
                resourceTypeId[7] == (byte)'e' &&
                resourceTypeId[8] == (byte)'d' &&
                resourceTypeId[9] == (byte)'_' &&
                resourceTypeId[10] == (byte)'f' &&
                resourceTypeId[11] == (byte)'o' &&
                resourceTypeId[12] == (byte)'o' &&
                resourceTypeId[13] == (byte)'d')
            {
                return true;
            }

            return false;
        }
    }
}
