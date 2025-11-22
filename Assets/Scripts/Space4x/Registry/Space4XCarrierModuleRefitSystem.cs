using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Processes carrier module refit queues deterministically and awards repair XP.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GameplayFixedStepSyncSystem))]
    public partial struct Space4XCarrierModuleRefitSystem : ISystem
    {
        private ComponentLookup<ModuleRefitFacility> _facilityLookup;
        private ComponentLookup<CrewSkills> _skillsLookup;
        private ComponentLookup<SkillExperienceGain> _xpLookup;
        private ComponentLookup<ModuleStatAggregate> _aggregateLookup;
        private ComponentLookup<ModuleSlotRequirement> _slotRequirementLookup;
        private EntityQuery _carrierQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GameplayFixedStep>();

            _facilityLookup = state.GetComponentLookup<ModuleRefitFacility>(true);
            _skillsLookup = state.GetComponentLookup<CrewSkills>(false);
            _xpLookup = state.GetComponentLookup<SkillExperienceGain>(false);
            _aggregateLookup = state.GetComponentLookup<ModuleStatAggregate>(true);
            _slotRequirementLookup = state.GetComponentLookup<ModuleSlotRequirement>(true);
            _carrierQuery = SystemAPI.QueryBuilder()
                .WithAll<CarrierModuleSlot, ModuleRefitRequest>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = SystemAPI.GetSingleton<GameplayFixedStep>().FixedDeltaTime;

            _facilityLookup.Update(ref state);
            _skillsLookup.Update(ref state);
            _xpLookup.Update(ref state);
            _aggregateLookup.Update(ref state);
            _slotRequirementLookup.Update(ref state);

            var hasSkillLog = SystemAPI.TryGetSingletonBuffer<SkillChangeLogEntry>(out var skillLog);
            var hasMaintenanceLog = SystemAPI.TryGetSingletonBuffer<ModuleMaintenanceCommandLogEntry>(out var maintenanceLog);
            var hasMaintenanceTelemetry = SystemAPI.TryGetSingletonEntity<ModuleMaintenanceTelemetry>(out var maintenanceTelemetryEntity);
            var maintenanceTelemetry = hasMaintenanceTelemetry ? state.EntityManager.GetComponentData<ModuleMaintenanceTelemetry>(maintenanceTelemetryEntity) : default;
            var telemetryDirty = false;
            var tick = time.Tick;

            using var carrierEntities = _carrierQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in carrierEntities)
            {
                if (!_facilityLookup.HasComponent(entity))
                {
                    continue;
                }

                var slots = SystemAPI.GetBuffer<CarrierModuleSlot>(entity);
                var requests = SystemAPI.GetBuffer<ModuleRefitRequest>(entity);

                if (requests.Length == 0)
                {
                    continue;
                }

                var facility = _facilityLookup[entity];
                var docked = state.EntityManager.HasComponent<DockedAtStation>(entity);
                if (facility.SupportsFieldRefit == 0 && !docked)
                {
                    continue;
                }

                var requestIndex = GetNextRequestIndex(requests);
                var request = requests[requestIndex];
                var slotIndex = FindSlotIndex(slots, request.SlotIndex);

                if (slotIndex < 0)
                {
                    requests.RemoveAt(requestIndex);
                    continue;
                }

                var slot = slots[slotIndex];
                if (!ValidateTarget(ref state, slot.SlotSize, request.TargetModule))
                {
                    requests.RemoveAt(requestIndex);
                    continue;
                }

                var requiredWork = math.max(0.1f, request.RequiredWork <= 0f ? 1f : request.RequiredWork);
                var starting = slot.RefitProgress <= 0f && slot.State != ModuleSlotState.Removing && slot.State != ModuleSlotState.Installing;
                slot.TargetModule = request.TargetModule;
                slot.State = request.TargetModule == Entity.Null ? ModuleSlotState.Removing : ModuleSlotState.Installing;

                if (starting)
                {
                    Space4XModuleMaintenanceUtility.LogEvent(hasMaintenanceLog, maintenanceLog, tick, entity, slot.SlotIndex, request.TargetModule, ModuleMaintenanceEventType.RefitStarted, requiredWork);
                    if (hasMaintenanceTelemetry)
                    {
                        telemetryDirty |= Space4XModuleMaintenanceUtility.ApplyTelemetry(ModuleMaintenanceEventType.RefitStarted, requiredWork, tick, ref maintenanceTelemetry);
                    }
                }

                var workDelta = ComputeWorkDelta(deltaTime, facility, GetRefitRateMultiplier(entity), GetRepairSkill(entity));
                slot.RefitProgress += workDelta;
                AwardRepairSkill(ref state, entity, workDelta, time.Tick, hasSkillLog ? skillLog : default);

                if (slot.RefitProgress + 1e-4f < requiredWork)
                {
                    slots[slotIndex] = slot;
                    continue;
                }

                slot.RefitProgress = 0f;
                slot.CurrentModule = request.TargetModule;
                slot.State = request.TargetModule == Entity.Null ? ModuleSlotState.Empty : ModuleSlotState.Active;
                slots[slotIndex] = slot;
                requests.RemoveAt(requestIndex);

                Space4XModuleMaintenanceUtility.LogEvent(hasMaintenanceLog, maintenanceLog, tick, entity, slot.SlotIndex, request.TargetModule, ModuleMaintenanceEventType.RefitCompleted, requiredWork);
                if (hasMaintenanceTelemetry)
                {
                    telemetryDirty |= Space4XModuleMaintenanceUtility.ApplyTelemetry(ModuleMaintenanceEventType.RefitCompleted, requiredWork, tick, ref maintenanceTelemetry);
                }
            }

            if (telemetryDirty && hasMaintenanceTelemetry)
            {
                state.EntityManager.SetComponentData(maintenanceTelemetryEntity, maintenanceTelemetry);
            }
        }

        private float GetRepairSkill(Entity entity)
        {
            return _skillsLookup.HasComponent(entity) ? math.saturate(_skillsLookup[entity].RepairSkill) : 0f;
        }

        private float GetRefitRateMultiplier(Entity entity)
        {
            return _aggregateLookup.HasComponent(entity) ? math.max(0f, _aggregateLookup[entity].RefitRateMultiplier) : 1f;
        }

        private bool ValidateTarget(ref SystemState state, ModuleSlotSize slotSize, Entity target)
        {
            if (target == Entity.Null)
            {
                return true;
            }

            if (!state.EntityManager.Exists(target))
            {
                return false;
            }

            if (_slotRequirementLookup.HasComponent(target))
            {
                return _slotRequirementLookup[target].SlotSize == slotSize;
            }

            return true;
        }

        private static int FindSlotIndex(DynamicBuffer<CarrierModuleSlot> slots, int slotIndex)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].SlotIndex == slotIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int GetNextRequestIndex(DynamicBuffer<ModuleRefitRequest> requests)
        {
            var bestIndex = 0;
            for (var i = 1; i < requests.Length; i++)
            {
                if (Compare(requests[i], requests[bestIndex]) < 0)
                {
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static int Compare(in ModuleRefitRequest a, in ModuleRefitRequest b)
        {
            var priority = a.Priority.CompareTo(b.Priority);
            if (priority != 0)
            {
                return priority;
            }

            var tick = a.RequestTick.CompareTo(b.RequestTick);
            if (tick != 0)
            {
                return tick;
            }

            return a.SlotIndex.CompareTo(b.SlotIndex);
        }

        private static float ComputeWorkDelta(float deltaTime, in ModuleRefitFacility facility, float refitMultiplier, float repairSkill)
        {
            var rate = math.max(0f, facility.RefitRatePerSecond);
            var skillFactor = 1f + repairSkill * 0.75f;
            var multiplier = math.max(0.01f, refitMultiplier);
            return deltaTime * rate * skillFactor * multiplier;
        }

        private void AwardRepairSkill(ref SystemState state, Entity entity, float workDelta, uint tick, DynamicBuffer<SkillChangeLogEntry> skillLog)
        {
            if (workDelta <= 0f)
            {
                return;
            }

            EnsureSkillComponents(ref state, entity, tick);

            var xpData = _xpLookup[entity];
            var deltaXp = Space4XSkillUtility.ComputeDeltaXp(SkillDomain.Repair, workDelta);
            xpData.RepairXp += deltaXp;
            xpData.LastProcessedTick = tick;
            _xpLookup[entity] = xpData;

            var skills = _skillsLookup[entity];
            skills.RepairSkill = Space4XSkillUtility.XpToSkill(xpData.RepairXp);
            _skillsLookup[entity] = skills;

            if (skillLog.IsCreated)
            {
                skillLog.Add(new SkillChangeLogEntry
                {
                    Tick = tick,
                    TargetEntity = entity,
                    Domain = SkillDomain.Repair,
                    DeltaXp = deltaXp,
                    NewSkill = skills.RepairSkill
                });
            }
        }

        private void EnsureSkillComponents(ref SystemState state, Entity entity, uint tick)
        {
            var updated = false;

            if (!_xpLookup.HasComponent(entity))
            {
                state.EntityManager.AddComponentData(entity, new SkillExperienceGain
                {
                    LastProcessedTick = tick
                });
                updated = true;
            }

            if (!_skillsLookup.HasComponent(entity))
            {
                state.EntityManager.AddComponentData(entity, new CrewSkills());
                updated = true;
            }

            if (updated)
            {
                _xpLookup.Update(ref state);
                _skillsLookup.Update(ref state);
            }
        }
    }
}
