using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Repairs damaged modules in priority order, respecting field repair caps and crew skill.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XComponentDegradationSystem))]
    public partial struct Space4XFieldRepairSystem : ISystem
    {
        private ComponentLookup<ModuleHealth> _healthLookup;
        private ComponentLookup<CrewSkills> _skillsLookup;
        private ComponentLookup<SkillExperienceGain> _xpLookup;
        private ComponentLookup<ModuleStatAggregate> _aggregateLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private EntityQuery _tuningQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GameplayFixedStep>();

            _healthLookup = state.GetComponentLookup<ModuleHealth>(false);
            _skillsLookup = state.GetComponentLookup<CrewSkills>(false);
            _xpLookup = state.GetComponentLookup<SkillExperienceGain>(false);
            _aggregateLookup = state.GetComponentLookup<ModuleStatAggregate>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _tuningQuery = state.GetEntityQuery(ComponentType.ReadOnly<RefitRepairTuningSingleton>());
        }

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
            _statsLookup.Update(ref state);

            var hasSkillLog = SystemAPI.TryGetSingletonBuffer<SkillChangeLogEntry>(out var skillLog);
            var hasMaintenanceLog = SystemAPI.TryGetSingletonBuffer<ModuleMaintenanceCommandLogEntry>(out var maintenanceLog);
            var hasMaintenanceTelemetry = SystemAPI.TryGetSingletonEntity<ModuleMaintenanceTelemetry>(out var maintenanceTelemetryEntity);
            var maintenanceTelemetry = hasMaintenanceTelemetry ? state.EntityManager.GetComponentData<ModuleMaintenanceTelemetry>(maintenanceTelemetryEntity) : default;
            var telemetryDirty = false;
            var tick = time.Tick;

            bool hasTuning = ModuleCatalogUtility.TryGetTuning(_tuningQuery, out var tuning);
            
            foreach (var (slots, capability, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>, RefRO<FieldRepairCapability>>().WithEntityAccess())
            {
                var inFacility = state.EntityManager.HasComponent<InRefitFacilityTag>(entity);
                var repairRate = inFacility && hasTuning 
                    ? tuning.RepairRateEffPerSecStation 
                    : (hasTuning ? tuning.RepairRateEffPerSecField : 0.005f);
                
                var repairBudget = repairRate * deltaTime;
                var criticalBudget = repairRate * deltaTime;
                var skillFactor = 1f + GetRepairSkill(entity) * 0.75f;
                var moduleMultiplier = GetRepairMultiplier(entity);
                
                // Engineering stat boosts repair speed and efficiency
                float engineeringBonus = 1f;
                if (_statsLookup.HasComponent(entity))
                {
                    var stats = _statsLookup[entity];
                    engineeringBonus = 1f + (stats.Engineering / 100f) * 0.3f; // Up to 30% faster repair
                }

                repairBudget *= moduleMultiplier * skillFactor * engineeringBonus;
                criticalBudget *= moduleMultiplier * skillFactor * engineeringBonus;

                while (repairBudget > 0f || (capability.ValueRO.CanRepairCritical != 0 && criticalBudget > 0f))
                {
                    if (!TrySelectModule(slots, capability.ValueRO.CanRepairCritical != 0, out var module, out var health, out var slotIndex))
                    {
                        break;
                    }

                    var maxHealth = capability.ValueRO.CanRepairCritical != 0 ? health.MaxHealth : health.MaxFieldRepairHealth;
                    var budget = health.CurrentHealth <= 0f ? criticalBudget : repairBudget;
                    var efficiencyDelta = budget;
                    var currentEfficiency = health.CurrentHealth / math.max(0.01f, health.MaxHealth);
                    var toHeal = math.min(budget * health.MaxHealth, math.max(0f, maxHealth - health.CurrentHealth));

                    if (toHeal <= 0f)
                    {
                        // Nothing usable for this module, avoid tight loops.
                        health.Failed = (byte)(health.CurrentHealth <= 0f ? 1 : 0);
                        _healthLookup[module] = health;
                        break;
                    }

                    health.CurrentHealth = math.min(maxHealth, health.CurrentHealth + toHeal);
                    health.Failed = (byte)(health.CurrentHealth <= 0f ? 1 : 0);
                    _healthLookup[module] = health;

                    if (health.CurrentHealth > 0f)
                    {
                        AwardRepairSkill(ref state, entity, toHeal, time.Tick, hasSkillLog ? skillLog : default);
                    }

                    Space4XModuleMaintenanceUtility.LogEvent(hasMaintenanceLog, maintenanceLog, tick, entity, slotIndex, module, ModuleMaintenanceEventType.RepairApplied, toHeal);
                    if (hasMaintenanceTelemetry)
                    {
                        telemetryDirty |= Space4XModuleMaintenanceUtility.ApplyTelemetry(ModuleMaintenanceEventType.RepairApplied, toHeal, tick, ref maintenanceTelemetry);
                    }

                    if (health.CurrentHealth <= 0f)
                    {
                        criticalBudget = math.max(0f, criticalBudget - toHeal);
                    }
                    else
                    {
                        repairBudget = math.max(0f, repairBudget - toHeal);
                    }
                }
            }

            if (telemetryDirty && hasMaintenanceTelemetry)
            {
                state.EntityManager.SetComponentData(maintenanceTelemetryEntity, maintenanceTelemetry);
            }
        }

        private bool TrySelectModule(DynamicBuffer<CarrierModuleSlot> slots, bool canRepairCritical, out Entity module, out ModuleHealth health, out int slotIndex)
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
                var maxHealth = canRepairCritical ? candidateHealth.MaxHealth : candidateHealth.MaxFieldRepairHealth;
                if (maxHealth <= 0f || candidateHealth.CurrentHealth >= maxHealth - 1e-4f)
                {
                    continue;
                }

                if (candidateHealth.CurrentHealth <= 0f && !canRepairCritical)
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

            slotIndex = found ? bestSlot : -1;
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

            EnsureSkillComponents(ref state, entity, tick);

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
