using ScenarioState = PureDOTS.Runtime.ScenarioState;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Resources
{
    /// <summary>
    /// System that creates and manages resource requests.
    /// Agents/groups can create requests for resources they need.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ResourceRequestSystem : ISystem
    {
        private const uint DefaultRequestTtlTicks = 1000;
        private Entity _requestIdGeneratorEntity;
        private EntityQuery _requestIdGeneratorQuery;
        private ComponentLookup<LogisticsOrder> _orderLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ResourceTypeIndex>();
            _requestIdGeneratorQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRequestIdGenerator>()
                .Build();
            _orderLookup = state.GetComponentLookup<LogisticsOrder>(false);
            EnsureRequestIdGeneratorExists(ref state);
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

            _orderLookup.Update(ref state);

            var currentTick = tickTime.Tick;
            var entityManager = state.EntityManager;
            var hasStorehouseRegistry = SystemAPI.TryGetSingletonEntity<StorehouseRegistry>(out var storehouseRegistryEntity);
            DynamicBuffer<StorehouseRegistryEntry> storehouseEntries = default;
            if (hasStorehouseRegistry)
            {
                storehouseEntries = entityManager.GetBuffer<StorehouseRegistryEntry>(storehouseRegistryEntity);
            }

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var requests in SystemAPI.Query<DynamicBuffer<NeedRequest>>())
            {
                var requestsBuffer = requests;
                for (int i = requestsBuffer.Length - 1; i >= 0; i--)
                {
                    var request = requestsBuffer[i];

                    if (currentTick - request.CreatedTick > DefaultRequestTtlTicks)
                    {
                        if (request.OrderEntity != Entity.Null && _orderLookup.HasComponent(request.OrderEntity))
                        {
                            var cancelledOrder = _orderLookup[request.OrderEntity];
                            cancelledOrder.Status = LogisticsOrderStatus.Cancelled;
                            cancelledOrder.FailureReason = ShipmentFailureReason.Cancelled;
                            _orderLookup[request.OrderEntity] = cancelledOrder;
                        }

                        request.FailureReason = RequestFailureReason.Expired;
                        request.OrderEntity = Entity.Null;
                        requestsBuffer[i] = request;
                        continue;
                    }

                    if (request.OrderEntity != Entity.Null && !_orderLookup.HasComponent(request.OrderEntity))
                    {
                        request.OrderEntity = Entity.Null;
                        requestsBuffer[i] = request;
                    }

                    if (request.OrderEntity != Entity.Null)
                    {
                        var activeOrder = _orderLookup[request.OrderEntity];
                        if (activeOrder.Status == LogisticsOrderStatus.Delivered)
                        {
                            requestsBuffer.RemoveAt(i);
                            continue;
                        }

                        if (activeOrder.Status == LogisticsOrderStatus.Failed ||
                            activeOrder.Status == LogisticsOrderStatus.Cancelled)
                        {
                            request.FailureReason = activeOrder.Status == LogisticsOrderStatus.Cancelled
                                ? RequestFailureReason.Cancelled
                                : MapFailureReason(activeOrder.FailureReason);
                            request.OrderEntity = Entity.Null;
                            requestsBuffer[i] = request;
                            continue;
                        }
                    }

                    if (request.FailureReason != RequestFailureReason.None ||
                        request.OrderEntity != Entity.Null)
                    {
                        continue;
                    }

                    if (request.RequesterEntity == Entity.Null || !entityManager.Exists(request.RequesterEntity))
                    {
                        request.FailureReason = RequestFailureReason.InvalidRequester;
                        requestsBuffer[i] = request;
                        continue;
                    }

                    var destination = request.TargetEntity != Entity.Null
                        ? request.TargetEntity
                        : request.RequesterEntity;

                    if (destination == Entity.Null || !entityManager.Exists(destination))
                    {
                        request.FailureReason = RequestFailureReason.InvalidTarget;
                        requestsBuffer[i] = request;
                        continue;
                    }

                    if (request.Amount <= 0f)
                    {
                        request.FailureReason = RequestFailureReason.NoSupply;
                        requestsBuffer[i] = request;
                        continue;
                    }

                    var resourceId = new FixedString64Bytes(request.ResourceTypeId);
                    var resourceIndex = resourceTypeIndex.Catalog.Value.LookupIndex(resourceId);
                    if (resourceIndex < 0)
                    {
                        request.FailureReason = RequestFailureReason.NoSupply;
                        requestsBuffer[i] = request;
                        continue;
                    }

                    Entity sourceNode = Entity.Null;
                    if (hasStorehouseRegistry)
                    {
                        for (int s = 0; s < storehouseEntries.Length; s++)
                        {
                            var entry = storehouseEntries[s];
                            if (entry.StorehouseEntity == Entity.Null || !entityManager.Exists(entry.StorehouseEntity))
                            {
                                continue;
                            }

                            if (HasSufficientSupply(entry, (ushort)resourceIndex, request.Amount))
                            {
                                sourceNode = entry.StorehouseEntity;
                                break;
                            }
                        }
                    }

                    if (sourceNode == Entity.Null)
                    {
                        request.FailureReason = RequestFailureReason.NoSupply;
                        requestsBuffer[i] = request;
                        continue;
                    }

                    var priority = (byte)math.clamp((int)math.round(request.Priority), 0, 255);
                    ResourceLogisticsService.CreateOrder(
                        sourceNode,
                        destination,
                        resourceId,
                        (ushort)resourceIndex,
                        request.Amount,
                        LogisticsJobKind.Delivery,
                        tickTime.Tick,
                        priority,
                        out var logisticsOrder);

                    logisticsOrder.OrderId = ToOrderId(request.RequestId);
                    logisticsOrder.Status = LogisticsOrderStatus.Planning;

                    var orderEntity = ecb.CreateEntity();
                    ecb.AddComponent(orderEntity, logisticsOrder);

                    request.OrderEntity = orderEntity;
                    requestsBuffer[i] = request;
                }
            }

            ecb.Playback(state.EntityManager);
        }

        /// <summary>
        /// Creates a new resource request.
        /// </summary>
        public void CreateRequest(
            ref SystemState state,
            ref DynamicBuffer<NeedRequest> requests,
            FixedString32Bytes resourceTypeId,
            float amount,
            Entity requester,
            float priority,
            uint currentTick,
            Entity targetEntity = default)
        {
            var requestId = GetNextRequestId(ref state);
            requests.Add(new NeedRequest
            {
                ResourceTypeId = resourceTypeId,
                Amount = amount,
                RequesterEntity = requester,
                Priority = priority,
                CreatedTick = currentTick,
                TargetEntity = targetEntity,
                RequestId = requestId,
                OrderEntity = Entity.Null,
                FailureReason = RequestFailureReason.None
            });
        }

        private static RequestFailureReason MapFailureReason(ShipmentFailureReason reason)
        {
            switch (reason)
            {
                case ShipmentFailureReason.InvalidSource:
                case ShipmentFailureReason.NoInventory:
                    return RequestFailureReason.NoSupply;
                case ShipmentFailureReason.InvalidDestination:
                    return RequestFailureReason.InvalidTarget;
                case ShipmentFailureReason.StorageFull:
                case ShipmentFailureReason.NoCapacity:
                case ShipmentFailureReason.CapacityFull:
                    return RequestFailureReason.CapacityFull;
                case ShipmentFailureReason.ReservationExpired:
                    return RequestFailureReason.Expired;
                case ShipmentFailureReason.TransportLost:
                    return RequestFailureReason.ReservationFailed;
                case ShipmentFailureReason.RouteUnavailable:
                    return RequestFailureReason.RouteUnavailable;
                case ShipmentFailureReason.NoCarrier:
                    return RequestFailureReason.NoCarrier;
                case ShipmentFailureReason.InvalidContainer:
                    return RequestFailureReason.InvalidContainer;
                case ShipmentFailureReason.Cancelled:
                    return RequestFailureReason.Cancelled;
                default:
                    return RequestFailureReason.ReservationFailed;
            }
        }

        private static int ToOrderId(uint requestId)
        {
            return requestId > int.MaxValue ? int.MaxValue : (int)requestId;
        }

        private static bool HasSufficientSupply(
            in StorehouseRegistryEntry entry,
            ushort resourceTypeIndex,
            float amount)
        {
            var available = 0f;
            var hasAggregate = false;
            for (int i = 0; i < entry.TypeSummaries.Length; i++)
            {
                var summary = entry.TypeSummaries[i];
                if (summary.ResourceTypeIndex != resourceTypeIndex)
                {
                    continue;
                }

                if (summary.TierId == (byte)ResourceQualityTier.Unknown)
                {
                    available = summary.Stored - summary.Reserved;
                    hasAggregate = true;
                    break;
                }

                available += summary.Stored - summary.Reserved;
            }

            if (!hasAggregate && entry.TypeSummaries.Length == 0)
            {
                return false;
            }

            return available >= amount;
        }

        private void EnsureRequestIdGeneratorExists(ref SystemState state)
        {
            var em = state.EntityManager;
            if (_requestIdGeneratorEntity != Entity.Null && em.Exists(_requestIdGeneratorEntity))
            {
                return;
            }

            if (!_requestIdGeneratorQuery.IsEmptyIgnoreFilter)
            {
                _requestIdGeneratorEntity = _requestIdGeneratorQuery.GetSingletonEntity();
                return;
            }

            // Burst-safe: avoid params-based CreateEntity(ComponentType...) which allocates ComponentType[].
            _requestIdGeneratorEntity = em.CreateEntity();
            em.AddComponentData(_requestIdGeneratorEntity, new ResourceRequestIdGenerator
            {
                NextRequestId = 1
            });
        }

        private uint GetNextRequestId(ref SystemState state)
        {
            var em = state.EntityManager;
            
            if (_requestIdGeneratorEntity == Entity.Null || !em.Exists(_requestIdGeneratorEntity))
            {
                EnsureRequestIdGeneratorExists(ref state);
            }

            var generator = em.GetComponentData<ResourceRequestIdGenerator>(_requestIdGeneratorEntity);
            var requestId = generator.NextRequestId == 0 ? 1u : generator.NextRequestId;
            generator.NextRequestId = requestId + 1;
            em.SetComponentData(_requestIdGeneratorEntity, generator);
            return requestId;
        }
    }
}
