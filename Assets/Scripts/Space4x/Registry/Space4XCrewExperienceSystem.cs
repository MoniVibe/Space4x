using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Accumulates skill experience from mining/hauling command log entries and updates crew skill multipliers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    public partial struct Space4XCrewExperienceSystem : ISystem
    {
        private ComponentLookup<SkillExperienceGain> _xpLookup;
        private ComponentLookup<CrewSkills> _skillsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XMiningTimeSpine>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _xpLookup = state.GetComponentLookup<SkillExperienceGain>(false);
            _skillsLookup = state.GetComponentLookup<CrewSkills>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();

            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out var commandLog))
            {
                return;
            }

            _xpLookup.Update(ref state);
            _skillsLookup.Update(ref state);

            var tick = time.Tick;
            var capacity = math.max(1, commandLog.Length);
            var missingXp = new NativeHashSet<Entity>(capacity, Allocator.Temp);
            var missingSkills = new NativeHashSet<Entity>(capacity, Allocator.Temp);

            for (var i = 0; i < commandLog.Length; i++)
            {
                var command = commandLog[i];
                if (command.Tick != tick || command.Amount <= 0f)
                {
                    continue;
                }

                var entity = command.TargetEntity;
                if (entity == Entity.Null)
                {
                    continue;
                }

                if (!_xpLookup.HasComponent(entity))
                {
                    missingXp.Add(entity);
                }

                if (!_skillsLookup.HasComponent(entity))
                {
                    missingSkills.Add(entity);
                }
            }

            if (missingXp.Count > 0 || missingSkills.Count > 0)
            {
                var em = state.EntityManager;

                foreach (var entity in missingXp)
                {
                    em.AddComponentData(entity, new SkillExperienceGain
                    {
                        MiningXp = 0f,
                        HaulingXp = 0f,
                        CombatXp = 0f,
                        RepairXp = 0f,
                        ExplorationXp = 0f,
                        LastProcessedTick = tick
                    });
                }

                foreach (var entity in missingSkills)
                {
                    em.AddComponentData(entity, new CrewSkills());
                }

                _xpLookup.Update(ref state);
                _skillsLookup.Update(ref state);

                if (!SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out commandLog))
                {
                    missingXp.Dispose();
                    missingSkills.Dispose();
                    return;
                }
            }

            var hasSkillLog = SystemAPI.TryGetSingletonBuffer<SkillChangeLogEntry>(out var skillLog);

            for (var i = 0; i < commandLog.Length; i++)
            {
                var command = commandLog[i];
                if (command.Tick != tick)
                {
                    continue;
                }

                switch (command.CommandType)
                {
                    case MiningCommandType.Gather:
                        ApplyXp(command.TargetEntity, SkillDomain.Mining, command.Amount, tick, hasSkillLog ? skillLog : default);
                        break;
                    case MiningCommandType.Pickup:
                        ApplyXp(command.TargetEntity, SkillDomain.Hauling, command.Amount, tick, hasSkillLog ? skillLog : default);
                        break;
                }
            }

            missingXp.Dispose();
            missingSkills.Dispose();
        }

        private void ApplyXp(Entity entity, SkillDomain domain, float amount, uint tick, DynamicBuffer<SkillChangeLogEntry> skillLog)
        {
            if (entity == Entity.Null || amount <= 0f)
            {
                return;
            }

            if (!_xpLookup.HasComponent(entity) || !_skillsLookup.HasComponent(entity))
            {
                return;
            }

            var xpData = _xpLookup[entity];
            var deltaXp = Space4XSkillUtility.ComputeDeltaXp(domain, amount);

            switch (domain)
            {
                case SkillDomain.Mining:
                    xpData.MiningXp += deltaXp;
                    break;
                case SkillDomain.Hauling:
                    xpData.HaulingXp += deltaXp;
                    break;
                case SkillDomain.Combat:
                    xpData.CombatXp += deltaXp;
                    break;
                case SkillDomain.Repair:
                    xpData.RepairXp += deltaXp;
                    break;
                case SkillDomain.Exploration:
                    xpData.ExplorationXp += deltaXp;
                    break;
            }

            xpData.LastProcessedTick = tick;
            _xpLookup[entity] = xpData;

            var skills = _skillsLookup[entity];
            skills = ApplySkills(skills, xpData);
            _skillsLookup[entity] = skills;

            if (skillLog.IsCreated)
            {
                skillLog.Add(new SkillChangeLogEntry
                {
                    Tick = tick,
                    TargetEntity = entity,
                    Domain = domain,
                    DeltaXp = deltaXp,
                    NewSkill = GetSkillValue(skills, domain)
                });
            }
        }

        private static CrewSkills ApplySkills(CrewSkills skills, in SkillExperienceGain xp)
        {
            skills.MiningSkill = Space4XSkillUtility.XpToSkill(xp.MiningXp);
            skills.HaulingSkill = Space4XSkillUtility.XpToSkill(xp.HaulingXp);
            skills.CombatSkill = Space4XSkillUtility.XpToSkill(xp.CombatXp);
            skills.RepairSkill = Space4XSkillUtility.XpToSkill(xp.RepairXp);
            skills.ExplorationSkill = Space4XSkillUtility.XpToSkill(xp.ExplorationXp);
            return skills;
        }

        private static float GetSkillValue(in CrewSkills skills, SkillDomain domain)
        {
            return domain switch
            {
                SkillDomain.Mining => skills.MiningSkill,
                SkillDomain.Hauling => skills.HaulingSkill,
                SkillDomain.Combat => skills.CombatSkill,
                SkillDomain.Repair => skills.RepairSkill,
                SkillDomain.Exploration => skills.ExplorationSkill,
                _ => 0f
            };
        }
    }
}
