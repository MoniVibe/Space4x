using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Fully repairs modules when docked at a station with overhaul capability.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFieldRepairSystem))]
    public partial struct Space4XStationOverhaulSystem : ISystem
    {
        private ComponentLookup<ModuleHealth> _healthLookup;
        private ComponentLookup<CrewSkills> _skillsLookup;
        private ComponentLookup<SkillExperienceGain> _xpLookup;
        private ComponentLookup<ModuleStatAggregate> _aggregateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GameplayFixedStep>();

            _healthLookup = state.GetComponentLookup<ModuleHealth>(false);
            _skillsLookup = state.GetComponentLookup<CrewSkills>(false);
            _xpLookup = state.GetComponentLookup<SkillExperienceGain>(false);
            _aggregateLookup = state.GetComponentLookup<ModuleStatAggregate>(true);
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

            _healthLookup.Update(ref state);
            _skillsLookup.Update(ref state);
            _xpLookup.Update(ref state);
            _aggregateLookup.Update(ref state);

            var hasSkillLog = SystemAPI.TryGetSingletonBuffer<SkillChangeLogEntry>(out var skillLog);
            var hasMaintenanceLog = SystemAPI.TryGetSingletonBuffer<ModuleMaintenanceCommandLogEntry>(out var maintenanceLog);
            var hasMaintenanceTelemetry = SystemAPI.TryGetSingletonEntity<ModuleMaintenanceTelemetry>(out var maintenanceTelemetryEntity);
            var maintenanceTelemetry = hasMaintenanceTelemetry ? state.EntityManager.GetComponentData<ModuleMaintenanceTelemetry>(maintenanceTelemetryEntity) : default;
            var telemetryDirty = false;
            var tick = time.Tick;

            foreach (var (slots, facility, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>, RefRO<StationOverhaulFacility>>()
                         .WithAll<DockedAtStation>()
                         .WithEntityAccess())
            {
                var repairRate = facility.ValueRO.OverhaulRatePerSecond * deltaTime;
                var skillFactor = 1f + GetRepairSkill(entity) * 0.75f;
                var moduleMultiplier = GetRepairMultiplier(entity);
                var budget = repairRate * moduleMultiplier * skillFactor;

                while (budget > 0f && TrySelectModule(slots, out var module, out var health, out var slotIndex))
                {
                    var toHeal = math.min(budget, math.max(0f, health.MaxHealth - health.CurrentHealth));
                    if (toHeal <= 0f)
                    {
                        break;
                    }

                    health.CurrentHealth = math.min(health.MaxHealth, health.CurrentHealth + toHeal);
                    health.Failed = (byte)(health.CurrentHealth <= 0f ? 1 : 0);
                    _healthLookup[module] = health;

                    AwardRepairSkill(ref state, entity, toHeal, tick, hasSkillLog ? skillLog : default);

                    Space4XModuleMaintenanceUtility.LogEvent(hasMaintenanceLog, maintenanceLog, tick, entity, slotIndex, module, ModuleMaintenanceEventType.RepairApplied, toHeal);
                    if (hasMaintenanceTelemetry)
                    {
                        telemetryDirty |= Space4XModuleMaintenanceUtility.ApplyTelemetry(ModuleMaintenanceEventType.RepairApplied, toHeal, tick, ref maintenanceTelemetry);
                    }

                    budget = math.max(0f, budget - toHeal);
                }
            }

            if (telemetryDirty && hasMaintenanceTelemetry)
            {
                state.EntityManager.SetComponentData(maintenanceTelemetryEntity, maintenanceTelemetry);
            }
        }

        private bool TrySelectModule(DynamicBuffer<CarrierModuleSlot> slots, out Entity module, out ModuleHealth health, out int slotIndex)
        {
            module = Entity.Null;
            health = default;
            slotIndex = -1;
            var found = false;
            byte bestPriority = byte.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < slots.Length; i++)
            {
                var candidate = slots[i].CurrentModule;
                if (candidate == Entity.Null || !_healthLookup.HasComponent(candidate))
                {
                    continue;
                }

                var candidateHealth = _healthLookup[candidate];
                if (candidateHealth.CurrentHealth >= candidateHealth.MaxHealth - 1e-4f)
                {
                    continue;
                }

                var priority = candidateHealth.RepairPriority;
                if (priority < bestPriority || (priority == bestPriority && slots[i].SlotIndex < bestSlot))
                {
                    bestPriority = priority;
                    bestSlot = slots[i].SlotIndex;
                    module = candidate;
                    health = candidateHealth;
                    found = true;
                }
            }

            slotIndex = bestSlot;
            return found;
        }

        private float GetRepairSkill(Entity entity)
        {
            return _skillsLookup.HasComponent(entity) ? math.saturate(_skillsLookup[entity].RepairSkill) : 0f;
        }

        private float GetRepairMultiplier(Entity entity)
        {
            return _aggregateLookup.HasComponent(entity) ? math.max(0f, _aggregateLookup[entity].RepairRateMultiplier) : 1f;
        }

        private void AwardRepairSkill(ref SystemState state, Entity entity, float amount, uint tick, DynamicBuffer<SkillChangeLogEntry> skillLog)
        {
            if (amount <= 0f)
            {
                return;
            }

            if (!_xpLookup.HasComponent(entity))
            {
                state.EntityManager.AddComponentData(entity, new SkillExperienceGain
                {
                    LastProcessedTick = tick
                });
                _xpLookup.Update(ref state);
            }

            if (!_skillsLookup.HasComponent(entity))
            {
                state.EntityManager.AddComponentData(entity, new CrewSkills());
                _skillsLookup.Update(ref state);
            }

            var xpData = _xpLookup[entity];
            var deltaXp = Space4XSkillUtility.ComputeDeltaXp(SkillDomain.Repair, amount);
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
    }
}
