using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct ResourceReservationBootstrapSystem : ISystem
    {
        private EntityQuery _resourceSourcesWithoutReservationQuery;
        private EntityQuery _storehousesWithoutReservationQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _resourceSourcesWithoutReservationQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<ResourceSourceConfig>() },
                None = new[] { ComponentType.ReadOnly<ResourceJobReservation>() }
            });

            _storehousesWithoutReservationQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<StorehouseConfig>() },
                None = new[] { ComponentType.ReadOnly<StorehouseJobReservation>() }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            state.CompleteDependency();

            using (var sources = _resourceSourcesWithoutReservationQuery.ToEntityArray(Allocator.TempJob))
            {
                for (var i = 0; i < sources.Length; i++)
                {
                    var entity = sources[i];
                    entityManager.AddComponent<ResourceJobReservation>(entity);
                    entityManager.AddBuffer<ResourceActiveTicket>(entity);
                }
            }

            using (var storehouses = _storehousesWithoutReservationQuery.ToEntityArray(Allocator.TempJob))
            {
                for (var i = 0; i < storehouses.Length; i++)
                {
                    var entity = storehouses[i];
                    entityManager.AddComponent<StorehouseJobReservation>(entity);
                    entityManager.AddBuffer<StorehouseReservationItem>(entity);
                }
            }

            if (_resourceSourcesWithoutReservationQuery.IsEmptyIgnoreFilter &&
                _storehousesWithoutReservationQuery.IsEmptyIgnoreFilter)
            {
                state.Enabled = false;
            }
        }
    }

    /// <summary>
    /// Handles villagers gathering resources from sources during normal simulation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    public partial struct ResourceGatheringSystem : ISystem
    {
        private EntityQuery _gathererQuery;
        private ComponentLookup<ResourceSourceState> _sourceStateLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ResourceTypeId> _resourceTypeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _gathererQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerJob, VillagerInventoryItem, VillagerFlags>()
                .WithNone<PlaybackGuardTag>()
                .Build();

            _sourceStateLookup = state.GetComponentLookup<ResourceSourceState>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _resourceTypeLookup = state.GetComponentLookup<ResourceTypeId>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate(_gathererQuery);
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

            _sourceStateLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _resourceTypeLookup.Update(ref state);

            // Gathering handled by VillagerJobExecutionSystem in the fixed-step job loop.
            // This system remains for compatibility but no longer mutates state.
        }
    }

    /// <summary>
    /// Handles villagers depositing gathered resources into nearby storehouses.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(ResourceGatheringSystem))]
    public partial struct ResourceDepositSystem : ISystem
    {
        private ComponentLookup<StorehouseConfig> _storehouseLookup;
        private BufferLookup<StorehouseCapacityElement> _capacityLookup;
        private BufferLookup<StorehouseInventoryItem> _storeItemsLookup;
        private ComponentLookup<StorehouseInventory> _storeInventoryLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _storehouseLookup = state.GetComponentLookup<StorehouseConfig>(true);
            _capacityLookup = state.GetBufferLookup<StorehouseCapacityElement>(false);
            _storeItemsLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);
            _storeInventoryLookup = state.GetComponentLookup<StorehouseInventory>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<VillagerJobTicket>();
            state.RequireForUpdate<VillagerJobCarryItem>();
            state.RequireForUpdate<ResourceTypeIndex>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out TimeState timeState) ||
                !SystemAPI.TryGetSingleton(out RewindState rewindState))
            {
                return;
            }
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            state.EntityManager.CompleteDependencyBeforeRW<VillagerAIState>();

            state.CompleteDependency();

            _storehouseLookup.Update(ref state);
            _capacityLookup.Update(ref state);
            _storeItemsLookup.Update(ref state);
            _storeInventoryLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var resourceCatalog = SystemAPI.GetSingleton<ResourceTypeIndex>();
            if (!resourceCatalog.Catalog.IsCreated)
            {
                return;
            }

            // Get resource interaction config or use defaults
            var config = SystemAPI.HasSingleton<ResourceInteractionConfig>()
                ? SystemAPI.GetSingleton<ResourceInteractionConfig>()
                : ResourceInteractionConfig.CreateDefaults();

            var depositJob = new DepositResourcesJob
            {
                StorehouseLookup = _storehouseLookup,
                CapacityLookup = _capacityLookup,
                StoreItemsLookup = _storeItemsLookup,
                StoreInventoryLookup = _storeInventoryLookup,
                TransformLookup = _transformLookup,
                DepositDistance = config.DepositDistance,
                CurrentTick = timeState.Tick,
                ResourceCatalog = resourceCatalog.Catalog
            };

            state.Dependency = depositJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct DepositResourcesJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<StorehouseConfig> StorehouseLookup;
            [ReadOnly] public BufferLookup<StorehouseCapacityElement> CapacityLookup;
            public BufferLookup<StorehouseInventoryItem> StoreItemsLookup;
            public ComponentLookup<StorehouseInventory> StoreInventoryLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public float DepositDistance;
            public uint CurrentTick;
            [ReadOnly] public BlobAssetReference<ResourceTypeIndexBlob> ResourceCatalog;

            public void Execute(
                ref DynamicBuffer<VillagerJobCarryItem> carry,
                ref VillagerJob job,
                ref VillagerJobTicket ticket,
                ref VillagerJobProgress progress,
                ref VillagerAIState aiState,
                in LocalTransform transform)
            {
                if (carry.Length == 0)
                {
                    return;
                }

                var totalCarried = 0f;
                for (var i = 0; i < carry.Length; i++)
                {
                    totalCarried += carry[i].Amount;
                }

                if (totalCarried <= 0f)
                {
                    return;
                }

                if (job.Phase != VillagerJob.JobPhase.Delivering || ticket.StorehouseEntity == Entity.Null)
                {
                    return;
                }

                var target = ticket.StorehouseEntity;
                if (target == Entity.Null ||
                    !StorehouseLookup.HasComponent(target) ||
                    !TransformLookup.HasComponent(target) ||
                    !CapacityLookup.HasBuffer(target) ||
                    !StoreItemsLookup.HasBuffer(target))
                {
                    return;
                }

                var storehousePos = TransformLookup[target].Position;
                if (math.distance(transform.Position, storehousePos) > DepositDistance)
                {
                    aiState.TargetEntity = target;
                    return;
                }

                var capacities = CapacityLookup[target];
                var storeItems = StoreItemsLookup[target];
                var hasInventoryComponent = StoreInventoryLookup.HasComponent(target);
                var storeInventory = hasInventoryComponent ? StoreInventoryLookup[target] : default;
                var inventoryModified = false;

                for (var i = carry.Length - 1; i >= 0; i--)
                {
                    var carried = carry[i];
                    if (carried.Amount <= 0f)
                    {
                        carry.RemoveAt(i);
                        continue;
                    }

                    var resourceId = ResolveResourceId(ResourceCatalog, carried.ResourceTypeIndex);
                    if (resourceId.Length == 0)
                    {
                        continue;
                    }

                    var maxCapacity = 0f;
                    for (var c = 0; c < capacities.Length; c++)
                    {
                        if (capacities[c].ResourceTypeId.Equals(resourceId))
                        {
                            maxCapacity = capacities[c].MaxCapacity;
                            break;
                        }
                    }

                    if (maxCapacity <= 0f)
                    {
                        continue;
                    }

                    var storeIndex = -1;
                    var currentStoredForType = 0f;
                    for (var s = 0; s < storeItems.Length; s++)
                    {
                        if (!storeItems[s].ResourceTypeId.Equals(resourceId))
                        {
                            continue;
                        }

                        currentStoredForType += storeItems[s].Amount;
                        if (storeItems[s].TierId == carried.TierId)
                        {
                            storeIndex = s;
                        }
                    }

                    var accepted = 0f;
                    if (storeIndex >= 0)
                    {
                        var storeItem = storeItems[storeIndex];
                        var capacityRemaining = math.max(0f, maxCapacity - currentStoredForType);
                        accepted = math.min(carried.Amount, capacityRemaining);
                        if (accepted > 0f)
                        {
                            var storedPayload = ResourcePayloadUtility.Create(carried.ResourceTypeIndex, storeItem.Amount, storeItem.TierId, storeItem.AverageQuality);
                            var incomingPayload = carried.AsPayload();
                            incomingPayload.Amount = accepted;
                            ResourcePayloadUtility.Merge(ref storedPayload, in incomingPayload);
                            storeItem.Amount = storedPayload.Amount;
                            storeItem.TierId = storedPayload.TierId;
                            storeItem.AverageQuality = storedPayload.AverageQuality;
                            storeItems[storeIndex] = storeItem;
                        }
                    }
                    else
                    {
                        var capacityRemaining = math.max(0f, maxCapacity - currentStoredForType);
                        accepted = math.min(carried.Amount, capacityRemaining);
                        if (accepted > 0f)
                        {
                            storeItems.Add(new StorehouseInventoryItem
                            {
                                ResourceTypeId = resourceId,
                                Amount = accepted,
                                Reserved = 0f,
                                TierId = carried.TierId,
                                AverageQuality = carried.AverageQuality
                            });
                        }
                    }

                    if (accepted <= 0f)
                    {
                        continue;
                    }

                    carried.Amount -= accepted;

                    if (carried.Amount <= 0f)
                    {
                        carry.RemoveAt(i);
                    }
                    else
                    {
                        carry[i] = carried;
                    }

                    if (hasInventoryComponent)
                    {
                        storeInventory.TotalStored += accepted;
                        storeInventory.LastUpdateTick = CurrentTick;
                        inventoryModified = true;
                    }

                    // Extension point: broadcast partial deliveries for future analytics/event systems.
                    progress.Delivered += accepted;
                }

                if (hasInventoryComponent && inventoryModified)
                {
                    storeInventory.ItemTypeCount = storeItems.Length;
                    StoreInventoryLookup[target] = storeInventory;
                }

                if (carry.Length == 0)
                {
                    job.Phase = VillagerJob.JobPhase.Completed;
                    job.ActiveTicketId = 0;
                    job.LastStateChangeTick = CurrentTick;

                    ticket.ResourceEntity = Entity.Null;
                    ticket.StorehouseEntity = Entity.Null;
                    ticket.TicketId = 0;
                    ticket.ReservedUnits = 0f;
                    ticket.Phase = (byte)VillagerJob.JobPhase.Completed;
                    ticket.LastProgressTick = CurrentTick;

                    progress.Gathered = 0f;
                    progress.TimeInPhase = 0f;
                    progress.LastUpdateTick = CurrentTick;

                    aiState.TargetEntity = Entity.Null;
                    aiState.CurrentGoal = VillagerAIState.Goal.None;
                    aiState.CurrentState = VillagerAIState.State.Idle;
                }
            }

            private static FixedString64Bytes ResolveResourceId(BlobAssetReference<ResourceTypeIndexBlob> catalog, ushort resourceTypeIndex)
            {
                if (!catalog.IsCreated)
                {
                    return default;
                }

                ref var blob = ref catalog.Value;
                if (resourceTypeIndex >= blob.Ids.Length)
                {
                    return default;
                }

                return blob.Ids[resourceTypeIndex];
            }

        }
    }

    /// <summary>
    /// Manages resource source respawn logic and infinite-source upkeep.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(ResourceDepositSystem))]
    public partial struct ResourceSourceManagementSystem : ISystem
    {
        private float _respawnCheckInterval;
        private uint _lastCheckTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _respawnCheckInterval = 1f;
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out TimeState timeState) ||
                !SystemAPI.TryGetSingleton(out RewindState rewindState))
            {
                return;
            }
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ticksSinceLastCheck = timeState.Tick - _lastCheckTick;
            var secondsSinceLastCheck = ticksSinceLastCheck * timeState.FixedDeltaTime;
            if (secondsSinceLastCheck < _respawnCheckInterval)
            {
                return;
            }

            _lastCheckTick = timeState.Tick;

            // Get resource interaction config or use defaults
            var interactionConfig = SystemAPI.HasSingleton<ResourceInteractionConfig>()
                ? SystemAPI.GetSingleton<ResourceInteractionConfig>()
                : ResourceInteractionConfig.CreateDefaults();

            var respawnJob = new RespawnResourcesJob
            {
                CurrentTick = timeState.Tick,
                FixedDeltaTime = timeState.FixedDeltaTime,
                InteractionConfig = interactionConfig
            };

            state.Dependency = respawnJob.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct RespawnResourcesJob : IJobEntity
        {
            public uint CurrentTick;
            public float FixedDeltaTime;
            public ResourceInteractionConfig InteractionConfig;

            public void Execute(ref ResourceSourceState state, in ResourceSourceConfig config, ref LastRecordedTick lastRespawn)
            {
                var isInfinite = (config.Flags & ResourceSourceConfig.FlagInfinite) != 0;
                if (isInfinite)
                {
                    state.UnitsRemaining = float.PositiveInfinity;
                    return;
                }

                var canRespawn = (config.Flags & ResourceSourceConfig.FlagRespawns) != 0;
                if (!canRespawn || state.UnitsRemaining > 0f)
                {
                    return;
                }

                // Use config passed from system
                var interactionConfig = InteractionConfig;

                var ticksSinceDepleted = CurrentTick - lastRespawn.Tick;
                var secondsSinceDepleted = ticksSinceDepleted * FixedDeltaTime;
                var minRespawnDelay = math.max(interactionConfig.MinRespawnDelaySeconds, config.RespawnSeconds);
                if (secondsSinceDepleted < minRespawnDelay)
                {
                    return;
                }

                state.UnitsRemaining = interactionConfig.DefaultRespawnUnits;
                lastRespawn.Tick = CurrentTick;
            }
        }
    }
}
