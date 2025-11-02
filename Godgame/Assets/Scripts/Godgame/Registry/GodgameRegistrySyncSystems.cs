using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Godgame.Registry
{
    /// <summary>
    /// Mirrors PureDOTS villager gameplay data into the Godgame-specific registry component.
    /// Ensures <see cref="GodgameVillager"/> reflects live state prior to bridge execution.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(GodgameRegistryBridgeSystem))]
    public partial struct GodgameVillagerSyncSystem : ISystem
    {
        private ComponentLookup<GodgameVillager> _villagerMirrorLookup;
        private ComponentLookup<VillagerAvailability> _availabilityLookup;
        private ComponentLookup<VillagerNeeds> _needsLookup;
        private ComponentLookup<VillagerMood> _moodLookup;
        private ComponentLookup<VillagerDisciplineState> _disciplineLookup;
        private ComponentLookup<VillagerAIState> _aiStateLookup;
        private ComponentLookup<VillagerCombatStats> _combatLookup;
        private ComponentLookup<VillagerJobTicket> _ticketLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerRegistry>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _villagerMirrorLookup = state.GetComponentLookup<GodgameVillager>(isReadOnly: false);
            _availabilityLookup = state.GetComponentLookup<VillagerAvailability>(isReadOnly: true);
            _needsLookup = state.GetComponentLookup<VillagerNeeds>(isReadOnly: true);
            _moodLookup = state.GetComponentLookup<VillagerMood>(isReadOnly: true);
            _disciplineLookup = state.GetComponentLookup<VillagerDisciplineState>(isReadOnly: true);
            _aiStateLookup = state.GetComponentLookup<VillagerAIState>(isReadOnly: true);
            _combatLookup = state.GetComponentLookup<VillagerCombatStats>(isReadOnly: true);
            _ticketLookup = state.GetComponentLookup<VillagerJobTicket>(isReadOnly: true);

            state.RequireForUpdate(SystemAPI.QueryBuilder()
                .WithAll<VillagerId, VillagerJob, LocalTransform>()
                .Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            _villagerMirrorLookup.Update(ref state);
            _availabilityLookup.Update(ref state);
            _needsLookup.Update(ref state);
            _moodLookup.Update(ref state);
            _disciplineLookup.Update(ref state);
            _aiStateLookup.Update(ref state);
            _combatLookup.Update(ref state);
            _ticketLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (villagerId, job, entity) in SystemAPI
                         .Query<RefRO<VillagerId>, RefRO<VillagerJob>>()
                         .WithAll<LocalTransform>()
                         .WithEntityAccess())
            {
                var hasMirror = _villagerMirrorLookup.HasComponent(entity);
                var mirror = hasMirror ? _villagerMirrorLookup[entity] : default;
                mirror.Discipline = VillagerDisciplineType.Unassigned;
                mirror.DisciplineLevel = 0;
                mirror.AIState = VillagerAIState.State.Idle;
                mirror.AIGoal = VillagerAIState.Goal.None;
                mirror.CurrentTarget = Entity.Null;

                mirror.VillagerId = villagerId.ValueRO.Value;
                mirror.FactionId = villagerId.ValueRO.FactionId;
                mirror.JobType = job.ValueRO.Type;
                mirror.JobPhase = job.ValueRO.Phase;
                mirror.ActiveTicketId = job.ValueRO.ActiveTicketId;
                mirror.Productivity = math.max(0f, job.ValueRO.Productivity);

                if (mirror.DisplayName.Length == 0)
                {
                    mirror.DisplayName = BuildVillagerName(mirror.VillagerId);
                }

                if (_availabilityLookup.HasComponent(entity))
                {
                    var availability = _availabilityLookup[entity];
                    mirror.IsAvailable = availability.IsAvailable;
                    mirror.IsReserved = availability.IsReserved;
                }
                else
                {
                    mirror.IsAvailable = 0;
                    mirror.IsReserved = 0;
                }

                if (_needsLookup.HasComponent(entity))
                {
                    var needs = _needsLookup[entity];
                    mirror.HealthPercent = needs.MaxHealth > 0f
                        ? math.saturate(needs.Health / math.max(0.0001f, needs.MaxHealth)) * 100f
                        : 0f;
                    mirror.EnergyPercent = math.clamp(needs.Energy, 0f, 100f);
                }
                else
                {
                    mirror.HealthPercent = 0f;
                    mirror.EnergyPercent = 0f;
                }

                if (_moodLookup.HasComponent(entity))
                {
                    var mood = _moodLookup[entity];
                    mirror.MoralePercent = math.clamp(mood.Mood, 0f, 100f);
                }
                else
                {
                    mirror.MoralePercent = 0f;
                }

                if (_disciplineLookup.HasComponent(entity))
                {
                    var discipline = _disciplineLookup[entity];
                    mirror.Discipline = discipline.Value;
                    mirror.DisciplineLevel = discipline.Level;
                }

                if (_aiStateLookup.HasComponent(entity))
                {
                    var ai = _aiStateLookup[entity];
                    mirror.AIState = ai.CurrentState;
                    mirror.AIGoal = ai.CurrentGoal;
                    mirror.CurrentTarget = ai.TargetEntity;
                }

                if (mirror.CurrentTarget == Entity.Null && _combatLookup.HasComponent(entity))
                {
                    var combat = _combatLookup[entity];
                    mirror.CurrentTarget = combat.CurrentTarget;
                    mirror.IsCombatReady = (byte)((combat.AttackDamage > 0f || combat.AttackSpeed > 0f) ? 1 : 0);
                }
                else if (!_combatLookup.HasComponent(entity))
                {
                    mirror.IsCombatReady = 0;
                }

                if (_ticketLookup.HasComponent(entity))
                {
                    var ticket = _ticketLookup[entity];
                    mirror.ActiveTicketId = ticket.TicketId != 0 ? ticket.TicketId : mirror.ActiveTicketId;
                    mirror.CurrentResourceTypeIndex = ticket.ResourceTypeIndex;
                }

                mirror.CurrentResourceTypeIndex = SanitizeResourceIndex(mirror.CurrentResourceTypeIndex);
                mirror.Productivity = math.max(0f, mirror.Productivity);

                if (!hasMirror)
                {
                    ecb.AddComponent(entity, mirror);
                }
                else
                {
                    _villagerMirrorLookup[entity] = mirror;
                }
            }
        }

        private static FixedString64Bytes BuildVillagerName(int villagerId)
        {
            FixedString64Bytes result = default;
            result.Append("Villager-");
            result.Append(villagerId);
            return result;
        }

        private static ushort SanitizeResourceIndex(ushort resourceTypeIndex)
        {
            return resourceTypeIndex == ushort.MaxValue ? (ushort)0 : resourceTypeIndex;
        }
    }

    /// <summary>
    /// Mirrors PureDOTS storehouse gameplay data into <see cref="GodgameStorehouse"/> components.
    /// Keeps per-resource summaries aligned with inventory and reservations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(GodgameRegistryBridgeSystem))]
    public partial struct GodgameStorehouseSyncSystem : ISystem
    {
        private ComponentLookup<GodgameStorehouse> _storehouseMirrorLookup;
        private ComponentLookup<StorehouseJobReservation> _reservationLookup;
        private BufferLookup<StorehouseReservationItem> _reservationItemsLookup;
        private BufferLookup<StorehouseCapacityElement> _capacityLookup;
        private BufferLookup<StorehouseInventoryItem> _inventoryItemsLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StorehouseRegistry>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _storehouseMirrorLookup = state.GetComponentLookup<GodgameStorehouse>(isReadOnly: false);
            _reservationLookup = state.GetComponentLookup<StorehouseJobReservation>(isReadOnly: true);
            _reservationItemsLookup = state.GetBufferLookup<StorehouseReservationItem>(isReadOnly: true);
            _capacityLookup = state.GetBufferLookup<StorehouseCapacityElement>(isReadOnly: true);
            _inventoryItemsLookup = state.GetBufferLookup<StorehouseInventoryItem>(isReadOnly: true);

            state.RequireForUpdate(SystemAPI.QueryBuilder()
                .WithAll<StorehouseConfig, StorehouseInventory, LocalTransform>()
                .Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            _storehouseMirrorLookup.Update(ref state);
            _reservationLookup.Update(ref state);
            _reservationItemsLookup.Update(ref state);
            _capacityLookup.Update(ref state);
            _inventoryItemsLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var hasResourceCatalog = SystemAPI.TryGetSingleton(out ResourceTypeIndex resourceTypeIndex) && resourceTypeIndex.Catalog.IsCreated;
            var catalog = hasResourceCatalog ? resourceTypeIndex.Catalog : default;

            foreach (var (inventory, entity) in SystemAPI
                         .Query<RefRO<StorehouseInventory>>()
                         .WithAll<StorehouseConfig, LocalTransform>()
                         .WithEntityAccess())
            {
                var hasMirror = _storehouseMirrorLookup.HasComponent(entity);
                var mirror = hasMirror ? _storehouseMirrorLookup[entity] : default;

                if (mirror.StorehouseId == 0)
                {
                    mirror.StorehouseId = entity.Index;
                }

                if (mirror.Label.Length == 0)
                {
                    mirror.Label = BuildStorehouseLabel(mirror.StorehouseId);
                }

                mirror.TotalCapacity = math.max(0f, inventory.ValueRO.TotalCapacity);
                mirror.TotalStored = math.max(0f, inventory.ValueRO.TotalStored);

                var totalReserved = 0f;
                var reservationItemsTotal = 0f;
                var lastMutationTick = inventory.ValueRO.LastUpdateTick;

                StorehouseJobReservation reservation = default;
                if (_reservationLookup.HasComponent(entity))
                {
                    reservation = _reservationLookup[entity];
                    totalReserved = reservation.ReservedCapacity;
                    lastMutationTick = math.max(lastMutationTick, reservation.LastMutationTick);
                }

                var summaries = default(FixedList32Bytes<GodgameStorehouseResourceSummary>);

                if (_capacityLookup.HasBuffer(entity))
                {
                    var capacities = _capacityLookup[entity];
                    for (var i = 0; i < capacities.Length; i++)
                    {
                        var capacity = capacities[i];
                        if (!TryResolveResourceTypeIndex(capacity.ResourceTypeId, hasResourceCatalog, catalog, out var typeIndex))
                        {
                            continue;
                        }

                        TryAddOrUpdateSummary(ref summaries, typeIndex, capacity.MaxCapacity, 0f, 0f);
                    }
                }

                if (_inventoryItemsLookup.HasBuffer(entity))
                {
                    var items = _inventoryItemsLookup[entity];
                    for (var i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        if (!TryResolveResourceTypeIndex(item.ResourceTypeId, hasResourceCatalog, catalog, out var typeIndex))
                        {
                            continue;
                        }

                        var index = FindSummaryIndex(ref summaries, typeIndex);
                        if (index >= 0)
                        {
                            var summary = summaries[index];
                            summary.Stored = item.Amount;
                            summary.Reserved = item.Reserved;
                            summaries[index] = summary;
                        }
                        else
                        {
                            TryAddOrUpdateSummary(ref summaries, typeIndex, 0f, item.Amount, item.Reserved);
                        }
                    }
                }

                if (_reservationItemsLookup.HasBuffer(entity))
                {
                    var reservationItems = _reservationItemsLookup[entity];
                    for (var i = 0; i < reservationItems.Length; i++)
                    {
                        var item = reservationItems[i];
                        reservationItemsTotal += item.Reserved;

                        var index = FindSummaryIndex(ref summaries, item.ResourceTypeIndex);
                        if (index >= 0)
                        {
                            var summary = summaries[index];
                            summary.Reserved += item.Reserved;
                            summaries[index] = summary;
                        }
                        else
                        {
                            TryAddOrUpdateSummary(ref summaries, item.ResourceTypeIndex, 0f, 0f, item.Reserved);
                        }
                    }
                }

                totalReserved = math.max(math.max(totalReserved, reservationItemsTotal), 0f);
                mirror.TotalReserved = totalReserved;

                mirror.ResourceSummaries = summaries.Length > 0 ? summaries : default;
                mirror.LastMutationTick = lastMutationTick != 0 ? lastMutationTick : mirror.LastMutationTick;

                if (summaries.Length > 0)
                {
                    mirror.PrimaryResourceTypeIndex = summaries[0].ResourceTypeIndex;
                    float bestScore = summaries[0].Stored + summaries[0].Reserved;

                    for (var i = 1; i < summaries.Length; i++)
                    {
                        var summary = summaries[i];
                        var score = summary.Stored + summary.Reserved;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            mirror.PrimaryResourceTypeIndex = summary.ResourceTypeIndex;
                        }
                    }
                }
                else
                {
                    mirror.PrimaryResourceTypeIndex = 0;
                }

                if (!hasMirror)
                {
                    ecb.AddComponent(entity, mirror);
                }
                else
                {
                    _storehouseMirrorLookup[entity] = mirror;
                }
            }
        }

        private static bool TryResolveResourceTypeIndex(FixedString64Bytes resourceTypeId, bool hasCatalog, BlobAssetReference<ResourceTypeIndexBlob> catalog, out ushort index)
        {
            if (resourceTypeId.Length == 0)
            {
                index = 0;
                return false;
            }

            if (hasCatalog)
            {
                var lookup = catalog.Value.LookupIndex(resourceTypeId);
                if (lookup >= 0)
                {
                    index = (ushort)lookup;
                    return true;
                }
            }

            index = 0;
            return false;
        }

        private static void TryAddOrUpdateSummary(ref FixedList32Bytes<GodgameStorehouseResourceSummary> summaries, ushort typeIndex, float capacity, float stored, float reserved)
        {
            var existingIndex = FindSummaryIndex(ref summaries, typeIndex);
            if (existingIndex >= 0)
            {
                var existing = summaries[existingIndex];
                existing.Capacity = math.max(existing.Capacity, capacity);
                existing.Stored = stored > 0f ? stored : existing.Stored;
                existing.Reserved += reserved;
                summaries[existingIndex] = existing;
                return;
            }

            if (summaries.Length >= summaries.Capacity)
            {
                return;
            }

            summaries.Add(new GodgameStorehouseResourceSummary
            {
                ResourceTypeIndex = typeIndex,
                Capacity = capacity,
                Stored = stored,
                Reserved = reserved
            });
        }

        private static int FindSummaryIndex(ref FixedList32Bytes<GodgameStorehouseResourceSummary> summaries, ushort resourceTypeIndex)
        {
            for (var i = 0; i < summaries.Length; i++)
            {
                if (summaries[i].ResourceTypeIndex == resourceTypeIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        private static FixedString64Bytes BuildStorehouseLabel(int storehouseId)
        {
            FixedString64Bytes result = default;
            result.Append("Storehouse-");
            result.Append(storehouseId);
            return result;
        }
    }
}

