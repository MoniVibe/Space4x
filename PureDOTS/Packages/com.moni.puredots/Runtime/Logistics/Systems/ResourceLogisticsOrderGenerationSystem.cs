using System;
using ScenarioState = PureDOTS.Runtime.ScenarioState;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Generates logistics orders from demand sources (construction sites, storehouses, consumption points).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ResourceLogisticsPlanningSystem))]
    public partial struct ResourceLogisticsOrderGenerationSystem : ISystem
    {
        private int _nextOrderId;
        private BufferLookup<ConstructionCostElement> _costBufferLookup;
        private BufferLookup<ConstructionDeliveredElement> _deliveredBufferLookup;
        private BufferLookup<StorehouseInventoryItem> _inventoryItemLookup;
        private BufferLookup<StorehouseCapacityElement> _capacityLookup;
        private ComponentLookup<LogisticsOrder> _orderLookup;
        private ComponentLookup<LogisticsNode> _nodeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ConstructionSiteProgress>();
            state.RequireForUpdate<ResourceTypeIndex>();
            _costBufferLookup = state.GetBufferLookup<ConstructionCostElement>(true);
            _deliveredBufferLookup = state.GetBufferLookup<ConstructionDeliveredElement>(true);
            _inventoryItemLookup = state.GetBufferLookup<StorehouseInventoryItem>(true);
            _capacityLookup = state.GetBufferLookup<StorehouseCapacityElement>(true);
            _orderLookup = state.GetComponentLookup<LogisticsOrder>(false);
            _nodeLookup = state.GetComponentLookup<LogisticsNode>(false);
            _nextOrderId = 1;
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

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ResourceTypeIndex>(out var resourceTypeIndex) ||
                !resourceTypeIndex.Catalog.IsCreated)
            {
                return;
            }

            _costBufferLookup.Update(ref state);
            _deliveredBufferLookup.Update(ref state);
            _inventoryItemLookup.Update(ref state);
            _capacityLookup.Update(ref state);
            _orderLookup.Update(ref state);
            _nodeLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Build map of destination+resource → hasActiveOrder to prevent duplicates
            var activeOrdersByDestinationAndResource = new NativeHashMap<OrderKey, bool>(64, Allocator.Temp);

            foreach (var (order, _) in SystemAPI.Query<RefRO<LogisticsOrder>>()
                .WithEntityAccess())
            {
                if (order.ValueRO.Status != LogisticsOrderStatus.Delivered &&
                    order.ValueRO.Status != LogisticsOrderStatus.Cancelled &&
                    order.ValueRO.Status != LogisticsOrderStatus.Failed)
                {
                    var resourceIndex = ResolveResourceTypeIndex(order.ValueRO, resourceTypeIndex.Catalog);
                    if (resourceIndex == ushort.MaxValue)
                    {
                        continue;
                    }

                    var key = new OrderKey
                    {
                        Destination = order.ValueRO.DestinationNode,
                        ResourceTypeIndex = resourceIndex
                    };
                    activeOrdersByDestinationAndResource.TryAdd(key, true);
                }
            }

            // Generate orders from construction sites
            foreach (var (construction, siteEntity) in SystemAPI.Query<RefRO<ConstructionSiteProgress>>()
                .WithEntityAccess())
            {
                // Skip completed construction sites
                if (construction.ValueRO.CurrentProgress >= construction.ValueRO.RequiredProgress)
                {
                    continue;
                }

                if (!_costBufferLookup.HasBuffer(siteEntity) || !_deliveredBufferLookup.HasBuffer(siteEntity))
                {
                    continue;
                }

                var costBuffer = _costBufferLookup[siteEntity];
                var deliveredBuffer = _deliveredBufferLookup[siteEntity];

                // Find source node (storehouse) for this construction site
                Entity sourceNode = Entity.Null;
                foreach (var (node, nodeEntity) in SystemAPI.Query<RefRO<LogisticsNode>>()
                    .WithEntityAccess())
                {
                    if (node.ValueRO.Kind == NodeKind.Warehouse || node.ValueRO.Kind == NodeKind.Settlement)
                    {
                        sourceNode = nodeEntity;
                        break; // Use first available warehouse
                    }
                }

                // Compare required vs delivered for each resource
                for (int i = 0; i < costBuffer.Length; i++)
                {
                    var cost = costBuffer[i];
                    float delivered = 0f;

                    // Find delivered amount for this resource
                    for (int j = 0; j < deliveredBuffer.Length; j++)
                    {
                        if (deliveredBuffer[j].ResourceTypeId.Equals(cost.ResourceTypeId))
                        {
                            delivered = deliveredBuffer[j].UnitsDelivered;
                            break;
                        }
                    }

                    float shortage = cost.UnitsRequired - delivered;
                    if (shortage > 0.01f) // Only create order if significant shortage
                    {
                        // Check if there's already an active order for this specific resource
                        var orderKey = new OrderKey
                        {
                            Destination = siteEntity,
                            ResourceTypeIndex = ResolveResourceTypeIndex(cost.ResourceTypeId, resourceTypeIndex.Catalog)
                        };

                        if (activeOrdersByDestinationAndResource.TryGetValue(orderKey, out bool hasOrder) && hasOrder)
                        {
                            continue; // Already has active order for this resource
                        }

                        // Create logistics order
                        if (orderKey.ResourceTypeIndex == ushort.MaxValue)
                        {
                            continue;
                        }

                        ResourceLogisticsService.CreateOrder(
                            sourceNode,
                            siteEntity,
                            cost.ResourceTypeId,
                            orderKey.ResourceTypeIndex,
                            shortage,
                            LogisticsJobKind.Supply,
                            tickTime.Tick,
                            128,
                            out var logisticsOrder); // Normal priority

                        logisticsOrder.OrderId = _nextOrderId++;

                        var orderEntity = ecb.CreateEntity();
                        ecb.AddComponent(orderEntity, logisticsOrder);

                        // Update active orders map
                        activeOrdersByDestinationAndResource.TryAdd(orderKey, true);
                    }
                }
            }

            // Generate orders from storehouses below threshold
            const float restockThreshold = 0.2f; // 20% of capacity

            foreach (var (inventory, storehouseEntity) in SystemAPI.Query<RefRO<StorehouseInventory>>()
                .WithEntityAccess())
            {
                if (!_inventoryItemLookup.HasBuffer(storehouseEntity) ||
                    !_capacityLookup.HasBuffer(storehouseEntity))
                {
                    continue;
                }

                var inventoryItems = _inventoryItemLookup[storehouseEntity];
                var capacities = _capacityLookup[storehouseEntity];

                // Check each resource type
                for (int i = 0; i < capacities.Length; i++)
                {
                    var capacity = capacities[i];
                    float currentAmount = 0f;

                    // Find current inventory amount
                    for (int j = 0; j < inventoryItems.Length; j++)
                    {
                        if (inventoryItems[j].ResourceTypeId.Equals(capacity.ResourceTypeId))
                        {
                            currentAmount = inventoryItems[j].Amount;
                            break;
                        }
                    }

                    // Check if below threshold
                    float threshold = capacity.MaxCapacity * restockThreshold;
                    if (currentAmount < threshold)
                    {
                        // Check if already has active order for this resource using map
                        var resourceIndex = ResolveResourceTypeIndex(capacity.ResourceTypeId, resourceTypeIndex.Catalog);
                        if (resourceIndex == ushort.MaxValue)
                        {
                            continue;
                        }

                        var orderKey = new OrderKey
                        {
                            Destination = storehouseEntity,
                            ResourceTypeIndex = resourceIndex
                        };

                        if (activeOrdersByDestinationAndResource.TryGetValue(orderKey, out bool hasOrder) && hasOrder)
                        {
                            continue; // Already has active order for this resource
                        }

                        // Find source node (another storehouse or resource source)
                        Entity sourceNode = Entity.Null;
                        foreach (var (node, nodeEntity) in SystemAPI.Query<RefRO<LogisticsNode>>()
                            .WithEntityAccess())
                        {
                            if (nodeEntity != storehouseEntity &&
                                (node.ValueRO.Kind == NodeKind.Warehouse || 
                                 node.ValueRO.Kind == NodeKind.Settlement ||
                                 node.ValueRO.Kind == NodeKind.TileCell))
                            {
                                sourceNode = nodeEntity;
                                break;
                            }
                        }

                        // Create restock order
                        float restockAmount = capacity.MaxCapacity * 0.8f - currentAmount; // Restock to 80%
                        if (restockAmount > 0.01f)
                        {
                            ResourceLogisticsService.CreateOrder(
                                sourceNode,
                                storehouseEntity,
                                capacity.ResourceTypeId,
                                resourceIndex,
                                restockAmount,
                                LogisticsJobKind.RedeployStock,
                                tickTime.Tick,
                                64,
                                out var logisticsOrder); // Lower priority than construction

                            logisticsOrder.OrderId = _nextOrderId++;

                            var orderEntity = ecb.CreateEntity();
                            ecb.AddComponent(orderEntity, logisticsOrder);

                            // Update active orders map
                            activeOrdersByDestinationAndResource.TryAdd(orderKey, true);
                        }
                    }
                }
            }

            activeOrdersByDestinationAndResource.Dispose();

            ecb.Playback(state.EntityManager);
        }

        private struct OrderKey : IEquatable<OrderKey>
        {
            public Entity Destination;
            public ushort ResourceTypeIndex;

            public bool Equals(OrderKey other)
            {
                return Destination.Equals(other.Destination) && ResourceTypeIndex == other.ResourceTypeIndex;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Destination, ResourceTypeIndex);
            }
        }

        private static ushort ResolveResourceTypeIndex(
            in LogisticsOrder order,
            BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            ref var ids = ref catalog.Value.Ids;
            var existingIndex = (int)order.ResourceTypeIndex;
            if (existingIndex >= 0 && existingIndex < ids.Length &&
                ids[existingIndex].Equals(order.ResourceId))
            {
                return order.ResourceTypeIndex;
            }

            var resolvedIndex = catalog.Value.LookupIndex(order.ResourceId);
            return resolvedIndex < 0 ? ushort.MaxValue : (ushort)resolvedIndex;
        }

        private static ushort ResolveResourceTypeIndex(
            in FixedString64Bytes resourceId,
            BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            var resolvedIndex = catalog.Value.LookupIndex(resourceId);
            return resolvedIndex < 0 ? ushort.MaxValue : (ushort)resolvedIndex;
        }
    }
}

