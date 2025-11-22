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
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XComponentDegradationSystem))]
    public partial struct Space4XFieldRepairSystem : ISystem
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

            foreach (var (slots, capability, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>, RefRO<FieldRepairCapability>>().WithEntityAccess())
            {
                var repairBudget = capability.ValueRO.RepairRatePerSecond * deltaTime;
                var criticalBudget = capability.ValueRO.CriticalRepairRate * deltaTime;
                var skillFactor = 1f + GetRepairSkill(entity) * 0.75f;
                var moduleMultiplier = GetRepairMultiplier(entity);

                repairBudget *= moduleMultiplier * skillFactor;
                criticalBudget *= moduleMultiplier * skillFactor;

                while (repairBudget > 0f || (capability.ValueRO.CanRepairCritical != 0 && criticalBudget > 0f))
                {
                    if (!TrySelectModule(slots, capability.ValueRO.CanRepairCritical != 0, out var module, out var health))
                    {
                        break;
                    }

                    var maxHealth = capability.ValueRO.CanRepairCritical != 0 ? health.MaxHealth : health.MaxFieldRepairHealth;
                    var budget = health.CurrentHealth <= 0f ? criticalBudget : repairBudget;
                    var toHeal = math.min(budget, math.max(0f, maxHealth - health.CurrentHealth));

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
        }

        private bool TrySelectModule(DynamicBuffer<CarrierModuleSlot> slots, bool canRepairCritical, out Entity module, out ModuleHealth health)
        {
            module = Entity.Null;
            health = default;
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
