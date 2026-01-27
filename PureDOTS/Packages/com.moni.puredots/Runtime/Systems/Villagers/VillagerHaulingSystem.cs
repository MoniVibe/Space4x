using PureDOTS.Config;
using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Construction;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Transport;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// Opportunistically assigns idle-capable villagers to haul construction materials
    /// by virtually moving goods from storehouses into construction deposit queues.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct VillagerHaulingSystem : ISystem
    {
        private BufferLookup<ConstructionMaterialNeed> _materialNeedLookup;
        private BufferLookup<ConstructionDepositCommand> _depositLookup;
        private ComponentLookup<ConstructionSiteId> _siteIdLookup;
        private BufferLookup<StorehouseInventoryItem> _storeItemsLookup;
        private ComponentLookup<StorehouseInventory> _storeInventoryLookup;
        private ComponentLookup<ConstructionSitePhaseSettings> _phaseSettingsLookup;
        private ComponentLookup<SquadCohesionState> _cohesionStateLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<GroupMembership> _membershipLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<StorehouseRegistry>();
            state.RequireForUpdate<ResourceTypeIndex>();
            state.RequireForUpdate<HaulingPolicyConfig>();

            _materialNeedLookup = state.GetBufferLookup<ConstructionMaterialNeed>(false);
            _depositLookup = state.GetBufferLookup<ConstructionDepositCommand>(false);
            _siteIdLookup = state.GetComponentLookup<ConstructionSiteId>(true);
            _storeItemsLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);
            _storeInventoryLookup = state.GetComponentLookup<StorehouseInventory>(false);
            _phaseSettingsLookup = state.GetComponentLookup<ConstructionSitePhaseSettings>(true);
            _cohesionStateLookup = state.GetComponentLookup<SquadCohesionState>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _membershipLookup = state.GetComponentLookup<GroupMembership>(true);
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

            var resourceIndex = SystemAPI.GetSingleton<ResourceTypeIndex>();
            if (!resourceIndex.Catalog.IsCreated)
            {
                return;
            }

            ref var catalog = ref resourceIndex.Catalog.Value;
            var policy = SystemAPI.GetSingleton<HaulingPolicyConfig>();

            var storehouseRegistryEntity = SystemAPI.GetSingletonEntity<StorehouseRegistry>();
            var storehouseEntries = state.EntityManager.GetBuffer<StorehouseRegistryEntry>(storehouseRegistryEntity);
            if (storehouseEntries.Length == 0)
            {
                return;
            }

            _materialNeedLookup.Update(ref state);
            _depositLookup.Update(ref state);
            _siteIdLookup.Update(ref state);
            _storeItemsLookup.Update(ref state);
            _storeInventoryLookup.Update(ref state);
            _phaseSettingsLookup.Update(ref state);
            _cohesionStateLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _membershipLookup.Update(ref state);

            // Gather outstanding needs into a list for deterministic scoring.
            var needCandidates = new NativeList<NeedCandidate>(Allocator.Temp);
            var outstandingMap = new NativeParallelHashMap<uint, float>(128, Allocator.Temp);

            foreach (var (siteId, transform, needsBuffer, entity) in SystemAPI.Query<RefRO<ConstructionSiteId>, RefRO<LocalTransform>, DynamicBuffer<ConstructionMaterialNeed>>().WithEntityAccess())
            {
                if (needsBuffer.Length == 0)
                {
                    continue;
                }

                var owner = _phaseSettingsLookup.HasComponent(entity)
                    ? _phaseSettingsLookup[entity].OwningGroup
                    : Entity.Null;

                for (int i = 0; i < needsBuffer.Length; i++)
                {
                    var need = needsBuffer[i];
                    var outstanding = math.max(0f, need.OutstandingUnits);
                    if (outstanding <= policy.MinUnitsPerHaul)
                    {
                        continue;
                    }

                    var candidate = new NeedCandidate
                    {
                        SiteEntity = entity,
                        Position = transform.ValueRO.Position,
                        ResourceTypeIndex = need.ResourceTypeIndex,
                        OutstandingUnits = outstanding,
                        Priority = need.Priority == 0 ? (byte)128 : need.Priority,
                        OwningGroup = owner
                    };
                    needCandidates.Add(candidate);

                    var key = ComputeNeedKey(entity, need.ResourceTypeIndex);
                    outstandingMap.TryAdd(key, outstanding);
                }
            }

            if (needCandidates.Length == 0)
            {
                needCandidates.Dispose();
                outstandingMap.Dispose();
                return;
            }

            ref var catalogIds = ref catalog.Ids;

            foreach (var (job, needs, behavior, archetype, flags, haulingState, entity) in
                     SystemAPI.Query<
                         RefRW<VillagerJob>,
                         RefRW<VillagerNeeds>,
                         RefRO<VillagerBehavior>,
                         RefRO<VillagerArchetypeResolved>,
                         RefRW<VillagerFlags>,
                         RefRW<VillagerHaulingState>>()
                         .WithEntityAccess())
            {
                if (!_transformLookup.HasComponent(entity) || !_membershipLookup.HasComponent(entity))
                {
                    continue;
                }

                var transform = _transformLookup[entity];
                var membership = _membershipLookup[entity];

                if (!IsEligibleForHauling(entity, in job.ValueRO))
                {
                    continue;
                }

                if (flags.ValueRO.IsDead || flags.ValueRO.IsWorking)
                {
                    continue;
                }

                if (timeState.Tick < haulingState.ValueRO.CooldownUntilTick)
                {
                    continue;
                }

                var currentEnergy = needs.ValueRO.EnergyFloat;
                if (currentEnergy < policy.MinEnergyToHaul)
                {
                    continue;
                }

                var willingness = ComputeWillingness(currentEnergy, in behavior.ValueRO, in archetype.ValueRO.Data);
                if (willingness < policy.MinimumWillingness)
                {
                    continue;
                }

                if (!TrySelectNeed(
                        transform.Position,
                        membership.Group,
                        needCandidates,
                        outstandingMap,
                        policy.MaxSiteChecks,
                        out var selectedNeed))
                {
                    continue;
                }

                if (!TrySelectStorehouse(
                        selectedNeed,
                        policy,
                        storehouseEntries,
                        ref catalogIds,
                        out var storehouseSelection))
                {
                    continue;
                }

                var resourceId = catalogIds[storehouseSelection.ResourceTypeIndex];
                if (!TryConsumeStorehouseInventory(
                        storehouseSelection,
                        resourceId,
                        policy,
                        ref _storeItemsLookup,
                        ref _storeInventoryLookup,
                        ref storehouseEntries,
                        out var consumedUnits))
                {
                    continue;
                }

                if (!_depositLookup.HasBuffer(selectedNeed.SiteEntity) || !_siteIdLookup.HasComponent(selectedNeed.SiteEntity))
                {
                    continue;
                }

                // Emit deposit command so ConstructionProgressSystem can ingest it.
                var depositBuffer = _depositLookup[selectedNeed.SiteEntity];
                var siteId = _siteIdLookup[selectedNeed.SiteEntity].Value;
                depositBuffer.Add(new ConstructionDepositCommand
                {
                    SiteId = siteId,
                    ResourceTypeId = resourceId,
                    Amount = consumedUnits
                });

                // Reduce outstanding bulletin immediately to avoid double-booking.
                if (_materialNeedLookup.HasBuffer(selectedNeed.SiteEntity))
                {
                    var needsBuffer = _materialNeedLookup[selectedNeed.SiteEntity];
                    for (int i = needsBuffer.Length - 1; i >= 0; i--)
                    {
                        if (needsBuffer[i].ResourceTypeIndex == selectedNeed.ResourceTypeIndex)
                        {
                            var updated = needsBuffer[i];
                            updated.OutstandingUnits = math.max(0f, updated.OutstandingUnits - consumedUnits);
                            if (updated.OutstandingUnits <= 0.01f)
                            {
                                needsBuffer.RemoveAt(i);
                            }
                            else
                            {
                                needsBuffer[i] = updated;
                            }
                            break;
                        }
                    }
                }

                var needKey = ComputeNeedKey(selectedNeed.SiteEntity, selectedNeed.ResourceTypeIndex);
                if (outstandingMap.TryGetValue(needKey, out var remainingOutstanding))
                {
                    outstandingMap[needKey] = math.max(0f, remainingOutstanding - consumedUnits);
                }

                // Apply effort cost / cooldown.
                var newEnergy = math.max(0f, currentEnergy - consumedUnits * policy.EnergyCostPerUnit);
                needs.ValueRW.SetEnergy(newEnergy);

                var stateRecord = haulingState.ValueRO;
                stateRecord.CooldownUntilTick = timeState.Tick + policy.CooldownTicks;
                stateRecord.LastSiteEntity = selectedNeed.SiteEntity;
                stateRecord.LastResourceTypeIndex = storehouseSelection.ResourceTypeIndex;
                stateRecord.LastUnits = consumedUnits;
                stateRecord.LastHaulTick = timeState.Tick;
                haulingState.ValueRW = stateRecord;

                var flagValue = flags.ValueRO;
                flagValue.IsWorking = true;
                flags.ValueRW = flagValue;
            }

            needCandidates.Dispose();
            outstandingMap.Dispose();
        }

        private bool IsEligibleForHauling(Entity villager, in VillagerJob job)
        {
            if (job.Type == VillagerJob.JobType.None)
            {
                return !IsInTightCohesion(villager);
            }

            return job.Phase == VillagerJob.JobPhase.Idle || job.Phase == VillagerJob.JobPhase.Completed;
        }

        private bool IsInTightCohesion(Entity villager)
        {
            if (!_membershipLookup.HasComponent(villager))
            {
                return false;
            }

            var membership = _membershipLookup[villager];
            if (membership.Group == Entity.Null || !_cohesionStateLookup.HasComponent(membership.Group))
            {
                return false;
            }

            var state = _cohesionStateLookup[membership.Group];
            return state.IsTight;
        }

        private static float ComputeWillingness(float energy, in VillagerBehavior behavior, in VillagerArchetypeData archetype)
        {
            var jobBias = math.saturate((archetype.GatherJobWeight + archetype.BuildJobWeight) * 0.005f);
            var energyFactor = math.saturate(energy * 0.01f);
            var diligence = math.saturate(0.5f + behavior.BoldScore * 0.005f);
            return jobBias * energyFactor * diligence;
        }

        private static uint ComputeNeedKey(Entity site, ushort resourceTypeIndex)
        {
            return math.hash(new int3(site.Index, site.Version, resourceTypeIndex));
        }

        private bool TrySelectNeed(
            float3 villagerPosition,
            Entity villagerGroup,
            NativeList<NeedCandidate> candidates,
            NativeParallelHashMap<uint, float> outstandingMap,
            byte maxChecks,
            out NeedCandidate selected)
        {
            selected = default;
            var bestScore = float.MinValue;
            var checks = math.min(maxChecks == 0 ? 8 : maxChecks, candidates.Length);
            var evaluated = 0;

            for (int i = 0; i < candidates.Length && evaluated < checks; i++)
            {
                var candidate = candidates[i];

                if (candidate.OwningGroup != Entity.Null && villagerGroup != Entity.Null && villagerGroup != candidate.OwningGroup)
                {
                    continue;
                }

                var key = ComputeNeedKey(candidate.SiteEntity, candidate.ResourceTypeIndex);
                if (outstandingMap.TryGetValue(key, out var remaining))
                {
                    if (remaining <= 0.01f)
                    {
                        continue;
                    }
                    candidate.OutstandingUnits = remaining;
                }

                var distanceSq = math.distancesq(villagerPosition, candidate.Position);
                var score = candidate.Priority * 0.01f + candidate.OutstandingUnits - distanceSq * 0.001f;

                if (score > bestScore)
                {
                    bestScore = score;
                    selected = candidate;
                }

                evaluated++;
            }

            return bestScore > float.MinValue;
        }

        private bool TrySelectStorehouse(
            in NeedCandidate need,
            in HaulingPolicyConfig policy,
            DynamicBuffer<StorehouseRegistryEntry> storehouseEntries,
            ref BlobArray<FixedString64Bytes> resourceIds,
            out StorehouseSelection selection)
        {
            selection = default;
            var bestScore = float.MinValue;

            for (int i = 0; i < storehouseEntries.Length; i++)
            {
                var entry = storehouseEntries[i];
                for (int s = 0; s < entry.TypeSummaries.Length; s++)
                {
                    var summary = entry.TypeSummaries[s];
                    if (summary.ResourceTypeIndex != need.ResourceTypeIndex)
                    {
                        continue;
                    }

                    var available = summary.Stored - summary.Reserved;
                    if (available <= policy.MinUnitsPerHaul)
                    {
                        continue;
                    }

                    var distance = math.distancesq(entry.Position, need.Position);
                    var score = available - distance * 0.001f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        selection = new StorehouseSelection
                        {
                            StorehouseEntity = entry.StorehouseEntity,
                            RegistryIndex = i,
                            SummaryIndex = s,
                            AvailableUnits = available,
                            ResourceTypeIndex = need.ResourceTypeIndex
                        };
                    }
                }
            }

            return bestScore > float.MinValue;
        }

        private bool TryConsumeStorehouseInventory(
            StorehouseSelection selection,
            in FixedString64Bytes resourceId,
            in HaulingPolicyConfig policy,
            ref BufferLookup<StorehouseInventoryItem> storeItemsLookup,
            ref ComponentLookup<StorehouseInventory> storeInventoryLookup,
            ref DynamicBuffer<StorehouseRegistryEntry> registryEntries,
            out float consumedUnits)
        {
            consumedUnits = 0f;

            if (!storeItemsLookup.HasBuffer(selection.StorehouseEntity))
            {
                return false;
            }

            var items = storeItemsLookup[selection.StorehouseEntity];
            var index = FindInventoryIndex(items, resourceId);
            if (index < 0)
            {
                return false;
            }

            var maxDesired = math.min(selection.AvailableUnits, policy.MaxUnitsPerHaul);
            maxDesired = math.max(policy.MinUnitsPerHaul, maxDesired);

            var item = items[index];
            var available = math.max(0f, item.Amount - item.Reserved);
            if (available <= 0f)
            {
                return false;
            }

            consumedUnits = math.min(maxDesired, available);
            if (consumedUnits <= policy.MinUnitsPerHaul)
            {
                return false;
            }

            item.Amount -= consumedUnits;
            items[index] = item;

            if (storeInventoryLookup.HasComponent(selection.StorehouseEntity))
            {
                var inventory = storeInventoryLookup[selection.StorehouseEntity];
                inventory.TotalStored = math.max(0f, inventory.TotalStored - consumedUnits);
                storeInventoryLookup[selection.StorehouseEntity] = inventory;
            }

            // Update registry summary for visibility; registry system will refresh later as well.
            if ((uint)selection.RegistryIndex < (uint)registryEntries.Length)
            {
                var entry = registryEntries[selection.RegistryIndex];
                if ((uint)selection.SummaryIndex < (uint)entry.TypeSummaries.Length)
                {
                    var summary = entry.TypeSummaries[selection.SummaryIndex];
                    summary.Stored = math.max(0f, summary.Stored - consumedUnits);
                    entry.TypeSummaries[selection.SummaryIndex] = summary;
                    registryEntries[selection.RegistryIndex] = entry;
                }
            }

            return true;
        }

        private static int FindInventoryIndex(DynamicBuffer<StorehouseInventoryItem> items, in FixedString64Bytes resourceId)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ResourceTypeId.Equals(resourceId))
                {
                    return i;
                }
            }
            return -1;
        }

        private struct NeedCandidate
        {
            public Entity SiteEntity;
            public float3 Position;
            public ushort ResourceTypeIndex;
            public float OutstandingUnits;
            public byte Priority;
            public Entity OwningGroup;
        }

        private struct StorehouseSelection
        {
            public Entity StorehouseEntity;
            public int RegistryIndex;
            public int SummaryIndex;
            public float AvailableUnits;
            public ushort ResourceTypeIndex;
        }
    }
}


