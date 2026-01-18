using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Resources;
using PureDOTS.Runtime.Scenarios;
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
        private ComponentLookup<ProductionDiagnostics> _productionDiagnosticsLookup;
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
            _productionDiagnosticsLookup = state.GetComponentLookup<ProductionDiagnostics>(false);
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
            _productionDiagnosticsLookup.Update(ref state);

            var useImmediateEcb = SystemAPI.HasSingleton<ScenarioRunnerTick>();
            var ecb = useImmediateEcb
                ? new EntityCommandBuffer(Allocator.Temp)
                : SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (facility, inventoryRW, items, capacities, reservations, queue, entity)
                     in SystemAPI.Query<RefRO<ProcessingFacility>, RefRW<StorehouseInventory>,
                         DynamicBuffer<StorehouseInventoryItem>, DynamicBuffer<StorehouseCapacityElement>,
                         DynamicBuffer<StorehouseReservationItem>, DynamicBuffer<ProcessingQueueEntry>>()
                         .WithEntityAccess())
            {
                var trackDiagnostics = _productionDiagnosticsLookup.HasComponent(entity);
                RefRW<ProductionDiagnostics> diagRef = default;
                if (trackDiagnostics)
                {
                    diagRef = _productionDiagnosticsLookup.GetRefRW(entity);
                }

                if (facility.ValueRO.IsActive == 0)
                {
                    continue;
                }

                var speedMultiplier = math.max(0.01f, (float)facility.ValueRO.SpeedMultiplier);

                if (_jobLookup.HasComponent(entity))
                {
                    var job = _jobLookup[entity];
                    if (trackDiagnostics)
                    {
                        diagRef.ValueRW.JobUpdateTicks++;
                    }
                    if (!TryGetRecipe(job.RecipeId, ref catalog, out ResourceRecipe activeRecipe))
                    {
                        if (trackDiagnostics)
                        {
                            diagRef.ValueRW.ActiveJobMissingRecipe++;
                        }
                        ecb.RemoveComponent<ProcessingJob>(entity);
                        continue;
                    }

                    UpdateActiveJob(
                        entity,
                        ref job,
                        in activeRecipe,
                        speedMultiplier,
                        tickTime.Tick,
                        deltaTime,
                        ref inventoryRW.ValueRW,
                        items,
                        capacities,
                        reservations,
                        resourceTypeIndex.Catalog,
                        ref ecb,
                        ref nextRequestId,
                        ref _needRequestLookup,
                        ref _productionDiagnosticsLookup);

                    if (_jobLookup.HasComponent(entity))
                    {
                        _jobLookup[entity] = job;
                    }
                    else if (trackDiagnostics)
                    {
                        diagRef.ValueRW.JobsCompleted++;
                    }

                    continue;
                }

                if (queue.Length == 0)
                {
                    continue;
                }

                if (!TryPickQueueEntry(queue, ref catalog, facility.ValueRO.Tier, out int entryIndex, out ResourceRecipe queuedRecipe,
                        out int removedInvalidBatch, out int removedMissingRecipe))
                {
                    if (trackDiagnostics)
                    {
                        diagRef.ValueRW.QueueRemovedInvalidBatch += removedInvalidBatch;
                        diagRef.ValueRW.QueueRemovedMissingRecipe += removedMissingRecipe;
                    }
                    continue;
                }

                if (trackDiagnostics)
                {
                    diagRef.ValueRW.QueueRemovedInvalidBatch += removedInvalidBatch;
                    diagRef.ValueRW.QueueRemovedMissingRecipe += removedMissingRecipe;
                }

                if (!HasOutputCapacity(in queuedRecipe, capacities, reservations, items, resourceTypeIndex.Catalog))
                {
                    if (trackDiagnostics)
                    {
                        diagRef.ValueRW.OutputCapacityBlocked++;
                    }
                    continue;
                }

                if (!HasInputs(in queuedRecipe, items, out float missingPrimary, out float missingSecondary))
                {
                    var addedRequests = EmitNeedRequests(
                        entity,
                        tickTime.Tick,
                        ref ecb,
                        in queuedRecipe,
                        missingPrimary,
                        missingSecondary,
                        queue[entryIndex].Priority,
                        ref nextRequestId,
                        ref _needRequestLookup);
                    if (trackDiagnostics)
                    {
                        diagRef.ValueRW.InputsMissing++;
                        diagRef.ValueRW.NeedRequestsEmitted += addedRequests;
                    }
                    continue;
                }

                if (!ConsumeInputs(in queuedRecipe, ref inventoryRW.ValueRW, items, resourceTypeIndex.Catalog))
                {
                    var addedRequests = EmitNeedRequests(
                        entity,
                        tickTime.Tick,
                        ref ecb,
                        in queuedRecipe,
                        queuedRecipe.PrimaryInput.Amount,
                        queuedRecipe.HasSecondaryInput != 0 ? queuedRecipe.SecondaryInput.Amount : 0f,
                        queue[entryIndex].Priority,
                        ref nextRequestId,
                        ref _needRequestLookup);
                    if (trackDiagnostics)
                    {
                        diagRef.ValueRW.InputsConsumeFailed++;
                        diagRef.ValueRW.NeedRequestsEmitted += addedRequests;
                    }
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
                if (trackDiagnostics)
                {
                    diagRef.ValueRW.JobsStarted++;
                }
                queue.RemoveAt(entryIndex);
            }

            if (useImmediateEcb)
            {
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }

            generator.NextRequestId = nextRequestId;
            state.EntityManager.SetComponentData(_requestIdGeneratorEntity, generator);
        }

        private static void UpdateActiveJob(
            Entity facilityEntity,
            ref ProcessingJob job,
            in ResourceRecipe recipe,
            float speedMultiplier,
            uint currentTick,
            float deltaTime,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items,
            DynamicBuffer<StorehouseCapacityElement> capacities,
            DynamicBuffer<StorehouseReservationItem> reservations,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            ref EntityCommandBuffer ecb,
            ref uint nextRequestId,
            ref BufferLookup<NeedRequest> needRequestLookup,
            ref ComponentLookup<ProductionDiagnostics> productionDiagnosticsLookup)
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

            if (productionDiagnosticsLookup.HasComponent(facilityEntity))
            {
                productionDiagnosticsLookup.GetRefRW(facilityEntity).ValueRW.JobDepositAttempts++;
            }

            if (!TryDepositOutputs(in recipe, ref inventory, items, capacities, reservations, catalog))
            {
                if (productionDiagnosticsLookup.HasComponent(facilityEntity))
                {
                    productionDiagnosticsLookup.GetRefRW(facilityEntity).ValueRW.OutputDepositFailed++;
                }
                job.RemainingTime = 0f;
                job.Progress = (half)1f;
                return;
            }

            if (productionDiagnosticsLookup.HasComponent(facilityEntity))
            {
                var diagRef = productionDiagnosticsLookup.GetRefRW(facilityEntity);
                diagRef.ValueRW.OutputDepositSuccess++;
                diagRef.ValueRW.JobsCompleted++;
            }

            job.BatchesCompleted++;
            if (job.BatchesCompleted >= job.BatchesQueued)
            {
                ecb.RemoveComponent<ProcessingJob>(facilityEntity);
                return;
            }

            if (!HasOutputCapacity(in recipe, capacities, reservations, items, catalog))
            {
                if (productionDiagnosticsLookup.HasComponent(facilityEntity))
                {
                    productionDiagnosticsLookup.GetRefRW(facilityEntity).ValueRW.OutputCapacityBlocked++;
                }
                job.RemainingTime = 0f;
                job.Progress = (half)0f;
                return;
            }

            if (!HasInputs(in recipe, items, out float missingPrimary, out float missingSecondary))
            {
                var addedRequests = EmitNeedRequests(
                    facilityEntity,
                    currentTick,
                    ref ecb,
                    in recipe,
                    missingPrimary,
                    missingSecondary,
                    128,
                    ref nextRequestId,
                    ref needRequestLookup);
                if (productionDiagnosticsLookup.HasComponent(facilityEntity))
                {
                    var diagRef = productionDiagnosticsLookup.GetRefRW(facilityEntity);
                    diagRef.ValueRW.InputsMissing++;
                    diagRef.ValueRW.NeedRequestsEmitted += addedRequests;
                }
                job.RemainingTime = 0f;
                job.Progress = (half)0f;
                return;
            }

            if (!ConsumeInputs(in recipe, ref inventory, items, catalog))
            {
                if (productionDiagnosticsLookup.HasComponent(facilityEntity))
                {
                    productionDiagnosticsLookup.GetRefRW(facilityEntity).ValueRW.InputsConsumeFailed++;
                }
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

        private static bool TryPickQueueEntry(
            DynamicBuffer<ProcessingQueueEntry> queue,
            ref ResourceChainCatalogBlob catalog,
            byte facilityTier,
            out int entryIndex,
            out ResourceRecipe recipe,
            out int removedInvalidBatch,
            out int removedMissingRecipe)
        {
            removedInvalidBatch = 0;
            removedMissingRecipe = 0;
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
                    removedInvalidBatch++;
                    i--;
                    continue;
                }

                if (!TryGetRecipe(entry.RecipeId, ref catalog, out ResourceRecipe candidate))
                {
                    queue.RemoveAt(i);
                    removedMissingRecipe++;
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
            DynamicBuffer<StorehouseInventoryItem> items,
            out float missingPrimary,
            out float missingSecondary)
        {
            missingPrimary = 0f;
            missingSecondary = 0f;

            var primaryId64 = ToFixed64(recipe.PrimaryInput.ResourceId);
            var primaryAvailable = GetUnreserved(items, primaryId64);
            missingPrimary = math.max(0f, recipe.PrimaryInput.Amount - primaryAvailable);

            if (recipe.HasSecondaryInput != 0)
            {
                var secondaryId64 = ToFixed64(recipe.SecondaryInput.ResourceId);
                var secondaryAvailable = GetUnreserved(items, secondaryId64);
                missingSecondary = math.max(0f, recipe.SecondaryInput.Amount - secondaryAvailable);
            }

            return missingPrimary <= 1e-3f && missingSecondary <= 1e-3f;
        }

        private static bool ConsumeInputs(
            in ResourceRecipe recipe,
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

        private static int EmitNeedRequests(
            Entity facilityEntity,
            uint currentTick,
            ref EntityCommandBuffer ecb,
            in ResourceRecipe recipe,
            float missingPrimary,
            float missingSecondary,
            byte queuePriority,
            ref uint nextRequestId,
            ref BufferLookup<NeedRequest> needRequestLookup)
        {
            var addedRequests = 0;
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
                addedRequests++;
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
                addedRequests++;
            }

            return addedRequests;
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
