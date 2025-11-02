using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
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
                .WithAll<VillagerJob, VillagerInventoryItem>()
                .WithNone<VillagerDeadTag, PlaybackGuardTag>()
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

            var gatherJob = new GatherResourcesJob
            {
                DeltaTime = timeState.FixedDeltaTime,
                SourceStateLookup = _sourceStateLookup,
                TransformLookup = _transformLookup,
                ResourceTypeLookup = _resourceTypeLookup,
                GatherDistance = 3f,
                BaseGatherRate = 10f
            };

            state.Dependency = gatherJob.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct GatherResourcesJob : IJobEntity
        {
            public float DeltaTime;
            public ComponentLookup<ResourceSourceState> SourceStateLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<ResourceTypeId> ResourceTypeLookup;
            public float GatherDistance;
            public float BaseGatherRate;

            public void Execute(
                ref VillagerJob job,
                ref DynamicBuffer<VillagerInventoryItem> inventory,
                in VillagerAIState aiState,
                in LocalTransform transform,
                in VillagerNeeds needs)
            {
                if (job.Type != VillagerJob.JobType.Gatherer ||
                    aiState.CurrentState != VillagerAIState.State.Working ||
                    job.WorksiteEntity == Entity.Null)
                {
                    return;
                }

                if (!SourceStateLookup.HasComponent(job.WorksiteEntity) ||
                    !TransformLookup.HasComponent(job.WorksiteEntity) ||
                    !ResourceTypeLookup.HasComponent(job.WorksiteEntity))
                {
                    job.WorksiteEntity = Entity.Null;
                    return;
                }

                var sourceTransform = TransformLookup[job.WorksiteEntity];
                var distance = math.distance(transform.Position, sourceTransform.Position);
                if (distance > GatherDistance)
                {
                    return;
                }

                var sourceState = SourceStateLookup[job.WorksiteEntity];
                var sourceTypeId = ResourceTypeLookup[job.WorksiteEntity].Value;
                if (sourceTypeId.IsEmpty || sourceState.UnitsRemaining <= 0f)
                {
                    job.WorksiteEntity = Entity.Null;
                    return;
                }

                var gatherAmount = BaseGatherRate * job.Productivity * DeltaTime;
                var energyMultiplier = math.saturate(needs.Energy / 50f);
                gatherAmount *= energyMultiplier;
                gatherAmount = math.min(gatherAmount, sourceState.UnitsRemaining);

                if (gatherAmount <= 0f)
                {
                    return;
                }

                sourceState.UnitsRemaining -= gatherAmount;
                SourceStateLookup[job.WorksiteEntity] = sourceState;

                var storedAmount = 0f;
                var foundInInventory = false;
                for (var i = 0; i < inventory.Length; i++)
                {
                    var item = inventory[i];
                    if (!item.ResourceTypeId.Equals(sourceTypeId))
                    {
                        continue;
                    }

                    var maxCarry = item.MaxCarryCapacity > 0f ? item.MaxCarryCapacity : 50f;
                    var capacityRemaining = math.max(0f, maxCarry - item.Amount);
                    var toStore = math.min(gatherAmount, capacityRemaining);
                    if (toStore > 0f)
                    {
                        item.Amount += toStore;
                        inventory[i] = item;
                        storedAmount += toStore;
                    }

                    foundInInventory = true;
                    break;
                }

                if (!foundInInventory && storedAmount < gatherAmount)
                {
                    var toStore = math.min(gatherAmount - storedAmount, 50f);
                    if (toStore > 0f)
                    {
                        inventory.Add(new VillagerInventoryItem
                        {
                            ResourceTypeId = sourceTypeId,
                            Amount = toStore,
                            MaxCarryCapacity = 50f
                        });
                        storedAmount += toStore;
                    }
                }

                job.WorkProgress += storedAmount / 100f;
            }
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

            _storehouseLookup.Update(ref state);
            _capacityLookup.Update(ref state);
            _storeItemsLookup.Update(ref state);
            _storeInventoryLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var depositJob = new DepositResourcesJob
            {
                StorehouseLookup = _storehouseLookup,
                CapacityLookup = _capacityLookup,
                StoreItemsLookup = _storeItemsLookup,
                StoreInventoryLookup = _storeInventoryLookup,
                TransformLookup = _transformLookup,
                DepositDistance = 5f,
                CurrentTick = timeState.Tick
            };

            state.Dependency = depositJob.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct DepositResourcesJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<StorehouseConfig> StorehouseLookup;
            public BufferLookup<StorehouseCapacityElement> CapacityLookup;
            public BufferLookup<StorehouseInventoryItem> StoreItemsLookup;
            public ComponentLookup<StorehouseInventory> StoreInventoryLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public float DepositDistance;
            public uint CurrentTick;

            public void Execute(
                ref DynamicBuffer<VillagerInventoryItem> inventory,
                ref VillagerJob job,
                in VillagerAIState aiState,
                in LocalTransform transform)
            {
                if (inventory.Length == 0)
                {
                    return;
                }

                var totalCarried = 0f;
                for (var i = 0; i < inventory.Length; i++)
                {
                    totalCarried += inventory[i].Amount;
                }

                if (totalCarried <= 0f)
                {
                    return;
                }

                // Simple heuristic: drop-off when idle (no active goal) and heavily loaded.
                var shouldDeposit = aiState.CurrentGoal == VillagerAIState.Goal.None && totalCarried >= 40f;
                if (!shouldDeposit)
                {
                    return;
                }

                var target = aiState.TargetEntity;
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
                    return;
                }

                var capacities = CapacityLookup[target];
                var storeItems = StoreItemsLookup[target];
                var hasInventoryComponent = StoreInventoryLookup.HasComponent(target);
                var storeInventory = hasInventoryComponent ? StoreInventoryLookup[target] : default;
                var inventoryModified = false;

                for (var i = inventory.Length - 1; i >= 0; i--)
                {
                    var carried = inventory[i];
                    if (carried.Amount <= 0f)
                    {
                        inventory.RemoveAt(i);
                        continue;
                    }

                    var maxCapacity = 0f;
                    for (var c = 0; c < capacities.Length; c++)
                    {
                        if (capacities[c].ResourceTypeId.Equals(carried.ResourceTypeId))
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
                    for (var s = 0; s < storeItems.Length; s++)
                    {
                        if (storeItems[s].ResourceTypeId.Equals(carried.ResourceTypeId))
                        {
                            storeIndex = s;
                            break;
                        }
                    }

                    var accepted = 0f;
                    if (storeIndex >= 0)
                    {
                        var storeItem = storeItems[storeIndex];
                        var capacityRemaining = math.max(0f, maxCapacity - storeItem.Amount);
                        accepted = math.min(carried.Amount, capacityRemaining);
                        if (accepted > 0f)
                        {
                            storeItem.Amount += accepted;
                            storeItems[storeIndex] = storeItem;
                        }
                    }
                    else
                    {
                        accepted = math.min(carried.Amount, maxCapacity);
                        if (accepted > 0f)
                        {
                            storeItems.Add(new StorehouseInventoryItem
                            {
                                ResourceTypeId = carried.ResourceTypeId,
                                Amount = accepted,
                                Reserved = 0f
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
                        inventory.RemoveAt(i);
                    }
                    else
                    {
                        inventory[i] = carried;
                    }

                    if (hasInventoryComponent)
                    {
                        storeInventory.TotalStored += accepted;
                        storeInventory.LastUpdateTick = CurrentTick;
                        inventoryModified = true;
                    }
                }

                if (hasInventoryComponent && inventoryModified)
                {
                    storeInventory.ItemTypeCount = storeItems.Length;
                    StoreInventoryLookup[target] = storeInventory;
                }
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
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
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

            var respawnJob = new RespawnResourcesJob
            {
                CurrentTick = timeState.Tick,
                FixedDeltaTime = timeState.FixedDeltaTime
            };

            state.Dependency = respawnJob.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct RespawnResourcesJob : IJobEntity
        {
            public uint CurrentTick;
            public float FixedDeltaTime;

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

                var ticksSinceDepleted = CurrentTick - lastRespawn.Tick;
                var secondsSinceDepleted = ticksSinceDepleted * FixedDeltaTime;
                if (secondsSinceDepleted < math.max(0.01f, config.RespawnSeconds))
                {
                    return;
                }

                state.UnitsRemaining = 100f;
                lastRespawn.Tick = CurrentTick;
            }
        }
    }
}
