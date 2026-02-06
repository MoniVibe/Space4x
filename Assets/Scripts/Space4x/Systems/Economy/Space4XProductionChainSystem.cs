using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Resources;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    public partial struct Space4XProductionChainSystem : ISystem
    {
        private ComponentLookup<ProcessingJob> _jobLookup;
        private BufferLookup<NeedRequest> _needRequestLookup;
        private Entity _requestIdGeneratorEntity;
        private EntityQuery _requestIdGeneratorQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourceChainCatalog>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ResourceTypeIndex>();
            state.RequireForUpdate<ProcessingFacility>();

            _jobLookup = state.GetComponentLookup<ProcessingJob>(false);
            _needRequestLookup = state.GetBufferLookup<NeedRequest>(false);
            _requestIdGeneratorQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRequestIdGenerator>()
                .Build();
            EnsureRequestIdGeneratorExists(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) &&
                (!scenario.IsInitialized || !scenario.EnableEconomy))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime) ||
                tickTime.IsPaused ||
                !tickTime.IsPlaying)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ResourceTypeIndex>(out var resourceTypeIndex) ||
                !resourceTypeIndex.Catalog.IsCreated)
            {
                return;
            }

            var catalogComponent = SystemAPI.GetSingleton<ResourceChainCatalog>();
            if (!catalogComponent.BlobReference.IsCreated)
            {
                return;
            }

            ref var catalog = ref catalogComponent.BlobReference.Value;
            var deltaTime = math.max(0f, tickTime.FixedDeltaTime);

            EnsureRequestIdGeneratorExists(ref state);
            var generator = state.EntityManager.GetComponentData<ResourceRequestIdGenerator>(_requestIdGeneratorEntity);
            var nextRequestId = generator.NextRequestId == 0 ? 1u : generator.NextRequestId;

            _jobLookup.Update(ref state);
            _needRequestLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (facility, inventoryRW, items, capacities, reservations, queue, entity)
                     in SystemAPI.Query<RefRO<ProcessingFacility>, RefRW<StorehouseInventory>,
                         DynamicBuffer<StorehouseInventoryItem>, DynamicBuffer<StorehouseCapacityElement>,
                         DynamicBuffer<StorehouseReservationItem>, DynamicBuffer<ProcessingQueueEntry>>()
                         .WithEntityAccess())
            {
                if (facility.ValueRO.IsActive == 0)
                {
                    continue;
                }

                var speedMultiplier = math.max(0.01f, (float)facility.ValueRO.SpeedMultiplier);
                var energyEfficiency = math.max(0.01f, (float)facility.ValueRO.EnergyEfficiency);

                if (_jobLookup.HasComponent(entity))
                {
                    var job = _jobLookup[entity];
                    if (!TryGetRecipe(job.RecipeId, ref catalog, out ResourceRecipe activeRecipe))
                    {
                        ecb.RemoveComponent<ProcessingJob>(entity);
                        continue;
                    }

                    UpdateActiveJob(
                        entity,
                        ref job,
                        in activeRecipe,
                        speedMultiplier,
                        energyEfficiency,
                        tickTime.Tick,
                        deltaTime,
                        ref inventoryRW.ValueRW,
                        items,
                        capacities,
                        reservations,
                        resourceTypeIndex.Catalog,
                        ref ecb,
                        ref nextRequestId,
                        ref _needRequestLookup);

                    if (_jobLookup.HasComponent(entity))
                    {
                        _jobLookup[entity] = job;
                    }

                    continue;
                }

                if (queue.Length == 0)
                {
                    continue;
                }

                if (!TryPickQueueEntry(queue, ref catalog, facility.ValueRO.Tier, out int entryIndex, out ResourceRecipe queuedRecipe))
                {
                    continue;
                }

                if (!HasOutputCapacity(in queuedRecipe, capacities, reservations, items, resourceTypeIndex.Catalog))
                {
                    continue;
                }

                var energyCost = GetEnergyCost(in queuedRecipe, energyEfficiency);
                if (!HasInputs(in queuedRecipe, energyCost, items, out float missingPrimary, out float missingSecondary, out float missingEnergy))
                {
                    EmitNeedRequests(
                        entity,
                        tickTime.Tick,
                        ref ecb,
                        in queuedRecipe,
                        missingPrimary,
                        missingSecondary,
                        missingEnergy,
                        queue[entryIndex].Priority,
                        ref nextRequestId,
                        ref _needRequestLookup);
                    continue;
                }

                if (!ConsumeInputs(in queuedRecipe, energyCost, ref inventoryRW.ValueRW, items, resourceTypeIndex.Catalog))
                {
                    EmitNeedRequests(
                        entity,
                        tickTime.Tick,
                        ref ecb,
                        in queuedRecipe,
                        queuedRecipe.PrimaryInput.Amount,
                        queuedRecipe.HasSecondaryInput != 0 ? queuedRecipe.SecondaryInput.Amount : 0f,
                        energyCost,
                        queue[entryIndex].Priority,
                        ref nextRequestId,
                        ref _needRequestLookup);
                    continue;
                }

                var entry = queue[entryIndex];
                var batches = math.max(1, entry.BatchCount);
                var duration = GetBatchDuration(in queuedRecipe, speedMultiplier);
                var jobComponent = new ProcessingJob
                {
                    RecipeId = entry.RecipeId,
                    RemainingTime = duration,
                    Progress = (half)0f,
                    StartTick = tickTime.Tick,
                    BatchesQueued = batches,
                    BatchesCompleted = 0
                };

                ecb.AddComponent(entity, jobComponent);
                queue.RemoveAt(entryIndex);
            }

            generator.NextRequestId = nextRequestId;
            state.EntityManager.SetComponentData(_requestIdGeneratorEntity, generator);
        }

        private static void UpdateActiveJob(
            Entity facilityEntity,
            ref ProcessingJob job,
            in ResourceRecipe recipe,
            float speedMultiplier,
            float energyEfficiency,
            uint currentTick,
            float deltaTime,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items,
            DynamicBuffer<StorehouseCapacityElement> capacities,
            DynamicBuffer<StorehouseReservationItem> reservations,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            ref EntityCommandBuffer ecb,
            ref uint nextRequestId,
            ref BufferLookup<NeedRequest> needRequestLookup)
        {
            var duration = GetBatchDuration(in recipe, speedMultiplier);
            if (job.RemainingTime > 0f)
            {
                job.RemainingTime = math.max(0f, job.RemainingTime - deltaTime);
                job.Progress = duration > 0f
                    ? (half)math.saturate(1f - (job.RemainingTime / duration))
                    : (half)1f;
            }

            if (job.RemainingTime > 0f)
            {
                return;
            }

            if (!TryDepositOutputs(in recipe, ref inventory, items, capacities, reservations, catalog))
            {
                job.RemainingTime = 0f;
                job.Progress = (half)1f;
                return;
            }

            job.BatchesCompleted++;
            if (job.BatchesCompleted >= job.BatchesQueued)
            {
                ecb.RemoveComponent<ProcessingJob>(facilityEntity);
                return;
            }

            if (!HasOutputCapacity(in recipe, capacities, reservations, items, catalog))
            {
                job.RemainingTime = 0f;
                job.Progress = (half)0f;
                return;
            }

            var energyCost = GetEnergyCost(in recipe, energyEfficiency);
            if (!HasInputs(in recipe, energyCost, items, out float missingPrimary, out float missingSecondary, out float missingEnergy))
            {
                EmitNeedRequests(
                    facilityEntity,
                    currentTick,
                    ref ecb,
                    in recipe,
                    missingPrimary,
                    missingSecondary,
                    missingEnergy,
                    128,
                    ref nextRequestId,
                    ref needRequestLookup);
                job.RemainingTime = 0f;
                job.Progress = (half)0f;
                return;
            }

            if (!ConsumeInputs(in recipe, energyCost, ref inventory, items, catalog))
            {
                job.RemainingTime = 0f;
                job.Progress = (half)0f;
                return;
            }

            job.StartTick = currentTick;
            job.RemainingTime = duration;
            job.Progress = (half)0f;
        }

        private static float GetBatchDuration(in ResourceRecipe recipe, float speedMultiplier)
        {
            var duration = math.max(0f, recipe.ProcessingTime);
            return speedMultiplier <= 0f ? duration : duration / speedMultiplier;
        }

        private static bool TryPickQueueEntry(
            DynamicBuffer<ProcessingQueueEntry> queue,
            ref ResourceChainCatalogBlob catalog,
            byte facilityTier,
            out int entryIndex,
            out ResourceRecipe recipe)
        {
            entryIndex = -1;
            recipe = default;

            byte bestPriority = byte.MaxValue;
            uint bestTick = uint.MaxValue;
            for (int i = 0; i < queue.Length; i++)
            {
                var entry = queue[i];
                if (entry.BatchCount <= 0)
                {
                    queue.RemoveAt(i);
                    i--;
                    continue;
                }

                if (!TryGetRecipe(entry.RecipeId, ref catalog, out ResourceRecipe candidate))
                {
                    queue.RemoveAt(i);
                    i--;
                    continue;
                }

                if (facilityTier < candidate.MinFacilityTier)
                {
                    continue;
                }

                if (entryIndex == -1 ||
                    entry.Priority < bestPriority ||
                    (entry.Priority == bestPriority && (entry.QueuedTick < bestTick ||
                                                        (entry.QueuedTick == bestTick && i < entryIndex))))
                {
                    entryIndex = i;
                    bestPriority = entry.Priority;
                    bestTick = entry.QueuedTick;
                    recipe = candidate;
                }
            }

            return entryIndex >= 0;
        }

        private static bool TryGetRecipe(FixedString32Bytes recipeId, ref ResourceChainCatalogBlob catalog, out ResourceRecipe recipe)
        {
            for (int i = 0; i < catalog.Recipes.Length; i++)
            {
                ref var candidate = ref catalog.Recipes[i];
                if (candidate.Id.Equals(recipeId))
                {
                    recipe = candidate;
                    return true;
                }
            }

            recipe = default;
            return false;
        }

        private static bool HasInputs(
            in ResourceRecipe recipe,
            float energyCost,
            DynamicBuffer<StorehouseInventoryItem> items,
            out float missingPrimary,
            out float missingSecondary,
            out float missingEnergy)
        {
            missingPrimary = 0f;
            missingSecondary = 0f;
            missingEnergy = 0f;

            var primaryId64 = ToFixed64(recipe.PrimaryInput.ResourceId);
            var primaryAvailable = GetUnreserved(items, primaryId64);
            missingPrimary = math.max(0f, recipe.PrimaryInput.Amount - primaryAvailable);

            if (recipe.HasSecondaryInput != 0)
            {
                var secondaryId64 = ToFixed64(recipe.SecondaryInput.ResourceId);
                var secondaryAvailable = GetUnreserved(items, secondaryId64);
                missingSecondary = math.max(0f, recipe.SecondaryInput.Amount - secondaryAvailable);
            }

            if (energyCost > 1e-3f)
            {
                var energyId = GetEnergyResourceId();
                var energyAvailable = GetUnreserved(items, ToFixed64(energyId));
                missingEnergy = math.max(0f, energyCost - energyAvailable);
            }

            return missingPrimary <= 1e-3f && missingSecondary <= 1e-3f && missingEnergy <= 1e-3f;
        }

        private static bool ConsumeInputs(
            in ResourceRecipe recipe,
            float energyCost,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items,
            BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            if (!TryResolveResourceTypeIndex(recipe.PrimaryInput.ResourceId, catalog, out ushort primaryIndex))
            {
                return false;
            }

            if (!StorehouseMutationService.TryConsumeUnreserved(primaryIndex, recipe.PrimaryInput.Amount, catalog, ref inventory, items))
            {
                return false;
            }

            if (recipe.HasSecondaryInput != 0)
            {
                if (!TryResolveResourceTypeIndex(recipe.SecondaryInput.ResourceId, catalog, out ushort secondaryIndex))
                {
                    return false;
                }

                if (!StorehouseMutationService.TryConsumeUnreserved(secondaryIndex, recipe.SecondaryInput.Amount, catalog, ref inventory, items))
                {
                    return false;
                }
            }

            if (energyCost > 1e-3f)
            {
                var energyId = GetEnergyResourceId();
                if (!TryResolveResourceTypeIndex(energyId, catalog, out ushort energyIndex))
                {
                    return false;
                }

                if (!StorehouseMutationService.TryConsumeUnreserved(energyIndex, energyCost, catalog, ref inventory, items))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasOutputCapacity(
            in ResourceRecipe recipe,
            DynamicBuffer<StorehouseCapacityElement> capacities,
            DynamicBuffer<StorehouseReservationItem> reservations,
            DynamicBuffer<StorehouseInventoryItem> items,
            BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            if (!TryResolveResourceTypeIndex(recipe.PrimaryOutput.ResourceId, catalog, out ushort primaryIndex))
            {
                return false;
            }

            var primaryAmount = math.max(0f, recipe.PrimaryOutput.Amount);
            if (recipe.HasSecondaryOutput == 0)
            {
                return primaryAmount <= 0f ||
                       StorehouseMutationService.HasCapacityForDeposit(primaryIndex, primaryAmount, catalog, items, capacities, reservations);
            }

            if (!TryResolveResourceTypeIndex(recipe.SecondaryOutput.ResourceId, catalog, out ushort secondaryIndex))
            {
                return false;
            }

            var secondaryAmount = math.max(0f, recipe.SecondaryOutput.Amount);
            if (primaryIndex == secondaryIndex)
            {
                var total = primaryAmount + secondaryAmount;
                return total <= 0f ||
                       StorehouseMutationService.HasCapacityForDeposit(primaryIndex, total, catalog, items, capacities, reservations);
            }

            if (primaryAmount > 0f &&
                !StorehouseMutationService.HasCapacityForDeposit(primaryIndex, primaryAmount, catalog, items, capacities, reservations))
            {
                return false;
            }

            if (secondaryAmount > 0f &&
                !StorehouseMutationService.HasCapacityForDeposit(secondaryIndex, secondaryAmount, catalog, items, capacities, reservations))
            {
                return false;
            }

            return true;
        }

        private static bool TryDepositOutputs(
            in ResourceRecipe recipe,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items,
            DynamicBuffer<StorehouseCapacityElement> capacities,
            DynamicBuffer<StorehouseReservationItem> reservations,
            BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            if (!HasOutputCapacity(in recipe, capacities, reservations, items, catalog))
            {
                return false;
            }

            if (!TryResolveResourceTypeIndex(recipe.PrimaryOutput.ResourceId, catalog, out ushort primaryIndex))
            {
                return false;
            }

            var primaryAmount = math.max(0f, recipe.PrimaryOutput.Amount);
            var secondaryAmount = math.max(0f, recipe.HasSecondaryOutput != 0 ? recipe.SecondaryOutput.Amount : 0f);
            ushort secondaryIndex;

            if (recipe.HasSecondaryOutput != 0 &&
                TryResolveResourceTypeIndex(recipe.SecondaryOutput.ResourceId, catalog, out secondaryIndex) &&
                primaryIndex == secondaryIndex)
            {
                var total = primaryAmount + secondaryAmount;
                return total <= 0f ||
                       StorehouseMutationService.TryDepositWithPerTypeCapacity(primaryIndex, total, catalog, ref inventory, items, capacities, reservations, out _);
            }

            if (primaryAmount > 0f &&
                !StorehouseMutationService.TryDepositWithPerTypeCapacity(primaryIndex, primaryAmount, catalog, ref inventory, items, capacities, reservations, out _))
            {
                return false;
            }

            if (recipe.HasSecondaryOutput != 0)
            {
                if (!TryResolveResourceTypeIndex(recipe.SecondaryOutput.ResourceId, catalog, out secondaryIndex))
                {
                    return false;
                }

                if (secondaryAmount > 0f &&
                    !StorehouseMutationService.TryDepositWithPerTypeCapacity(secondaryIndex, secondaryAmount, catalog, ref inventory, items, capacities, reservations, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private static void EmitNeedRequests(
            Entity facilityEntity,
            uint currentTick,
            ref EntityCommandBuffer ecb,
            in ResourceRecipe recipe,
            float missingPrimary,
            float missingSecondary,
            float missingEnergy,
            byte queuePriority,
            ref uint nextRequestId,
            ref BufferLookup<NeedRequest> needRequestLookup)
        {
            DynamicBuffer<NeedRequest> requests;
            if (needRequestLookup.HasBuffer(facilityEntity))
            {
                requests = needRequestLookup[facilityEntity];
            }
            else
            {
                requests = ecb.AddBuffer<NeedRequest>(facilityEntity);
            }

            var priority = math.max(0f, 255f - queuePriority);

            if (missingPrimary > 1e-3f &&
                !HasActiveNeedRequest(requests, recipe.PrimaryInput.ResourceId, facilityEntity))
            {
                var requestId = ConsumeRequestId(ref nextRequestId);
                requests.Add(new NeedRequest
                {
                    ResourceTypeId = recipe.PrimaryInput.ResourceId,
                    Amount = missingPrimary,
                    RequesterEntity = facilityEntity,
                    Priority = priority,
                    CreatedTick = currentTick,
                    TargetEntity = facilityEntity,
                    RequestId = requestId,
                    OrderEntity = Entity.Null,
                    FailureReason = RequestFailureReason.None
                });
            }

            if (recipe.HasSecondaryInput != 0 &&
                missingSecondary > 1e-3f &&
                !HasActiveNeedRequest(requests, recipe.SecondaryInput.ResourceId, facilityEntity))
            {
                var requestId = ConsumeRequestId(ref nextRequestId);
                requests.Add(new NeedRequest
                {
                    ResourceTypeId = recipe.SecondaryInput.ResourceId,
                    Amount = missingSecondary,
                    RequesterEntity = facilityEntity,
                    Priority = priority,
                    CreatedTick = currentTick,
                    TargetEntity = facilityEntity,
                    RequestId = requestId,
                    OrderEntity = Entity.Null,
                    FailureReason = RequestFailureReason.None
                });
            }

            if (missingEnergy > 1e-3f)
            {
                var energyId = GetEnergyResourceId();
                if (!HasActiveNeedRequest(requests, energyId, facilityEntity))
                {
                    var requestId = ConsumeRequestId(ref nextRequestId);
                    requests.Add(new NeedRequest
                    {
                        ResourceTypeId = energyId,
                        Amount = missingEnergy,
                        RequesterEntity = facilityEntity,
                        Priority = priority,
                        CreatedTick = currentTick,
                        TargetEntity = facilityEntity,
                        RequestId = requestId,
                        OrderEntity = Entity.Null,
                        FailureReason = RequestFailureReason.None
                    });
                }
            }
        }

        private static bool HasActiveNeedRequest(
            DynamicBuffer<NeedRequest> requests,
            FixedString32Bytes resourceId,
            Entity targetEntity)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (request.FailureReason != RequestFailureReason.None)
                {
                    continue;
                }

                if (request.TargetEntity != targetEntity)
                {
                    continue;
                }

                if (request.ResourceTypeId.Equals(resourceId))
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetUnreserved(DynamicBuffer<StorehouseInventoryItem> items, FixedString64Bytes resourceId)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item.ResourceTypeId.Equals(resourceId))
                {
                    return math.max(0f, item.Amount - item.Reserved);
                }
            }

            return 0f;
        }

        private static float GetEnergyCost(in ResourceRecipe recipe, float energyEfficiency)
        {
            var baseCost = math.max(0f, recipe.EnergyCost);
            if (baseCost <= 1e-3f)
            {
                return 0f;
            }

            var energyId = GetEnergyResourceId();
            if (recipe.PrimaryOutput.ResourceId.Equals(energyId) ||
                (recipe.HasSecondaryOutput != 0 && recipe.SecondaryOutput.ResourceId.Equals(energyId)))
            {
                return 0f;
            }

            return baseCost * math.max(0.01f, energyEfficiency);
        }

        private static FixedString32Bytes GetEnergyResourceId()
        {
            FixedString32Bytes id = default;
            id.Append('r');
            id.Append('e');
            id.Append('f');
            id.Append('i');
            id.Append('n');
            id.Append('e');
            id.Append('d');
            id.Append('_');
            id.Append('f');
            id.Append('u');
            id.Append('e');
            id.Append('l');
            id.Append('s');
            return id;
        }

        private static bool TryResolveResourceTypeIndex(
            FixedString32Bytes resourceId,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            out ushort index)
        {
            var resourceId64 = ToFixed64(resourceId);
            var resolved = catalog.Value.LookupIndex(resourceId64);
            if (resolved < 0)
            {
                index = ushort.MaxValue;
                return false;
            }

            index = (ushort)resolved;
            return true;
        }

        private static FixedString64Bytes ToFixed64(FixedString32Bytes value)
        {
            FixedString64Bytes result = default;
            for (int i = 0; i < value.Length && result.Length < result.Capacity; i++)
            {
                result.Append((char)value[i]);
            }
            return result;
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

            _requestIdGeneratorEntity = em.CreateEntity();
            em.AddComponentData(_requestIdGeneratorEntity, new ResourceRequestIdGenerator
            {
                NextRequestId = 1
            });
        }

        private static uint ConsumeRequestId(ref uint nextRequestId)
        {
            var requestId = nextRequestId == 0 ? 1u : nextRequestId;
            nextRequestId = requestId + 1;
            return requestId;
        }
    }
}
