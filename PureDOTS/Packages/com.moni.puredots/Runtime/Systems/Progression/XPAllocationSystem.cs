using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using PureDOTS.Runtime.Progression;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Systems.Progression
{
    /// <summary>
    /// System that processes XP awards and updates mastery levels.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct XPAllocationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process XP award requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<XPAwardRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                
                if (!SystemAPI.HasBuffer<SkillXP>(req.TargetEntity))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var skillBuffer = SystemAPI.GetBuffer<SkillXP>(req.TargetEntity);
                
                // Get XP multiplier if config exists
                float multiplier = 1f;
                if (SystemAPI.HasComponent<ProgressionConfig>(req.TargetEntity))
                {
                    multiplier = SystemAPI.GetComponent<ProgressionConfig>(req.TargetEntity).XPMultiplier;
                }

                uint adjustedAmount = (uint)(req.Amount * multiplier);

                bool masteryChanged = ProgressionHelpers.AwardXP(
                    ref skillBuffer,
                    req.Domain,
                    adjustedAmount,
                    currentTick,
                    out var oldMastery,
                    out var newMastery);

                // Emit XP gained event
                if (SystemAPI.HasBuffer<XPGainedEvent>(req.TargetEntity))
                {
                    var events = SystemAPI.GetBuffer<XPGainedEvent>(req.TargetEntity);
                    events.Add(new XPGainedEvent
                    {
                        Domain = req.Domain,
                        Amount = adjustedAmount,
                        OldMastery = oldMastery,
                        NewMastery = newMastery,
                        Tick = currentTick
                    });
                }

                // Update total XP and check for level up
                if (SystemAPI.HasComponent<CharacterProgression>(req.TargetEntity))
                {
                    var progression = SystemAPI.GetComponent<CharacterProgression>(req.TargetEntity);
                    progression.TotalXPEarned += adjustedAmount;
                    progression.CurrentLevelXP += adjustedAmount;

                    // Check for level up
                    while (progression.CurrentLevelXP >= progression.XPToNextLevel && 
                           progression.Level < GetMaxLevel(ref state, req.TargetEntity))
                    {
                        byte oldLevel = progression.Level;
                        progression.CurrentLevelXP -= progression.XPToNextLevel;
                        progression.Level++;

                        // Calculate new XP requirement
                        var config = GetProgressionConfig(ref state, req.TargetEntity);
                        progression.XPToNextLevel = ProgressionHelpers.CalculateXPForLevel(
                            (byte)(progression.Level + 1),
                            config.BaseXPPerLevel,
                            config.LevelXPScaling);

                        // Award points
                        byte skillPoints = ProgressionHelpers.CalculateSkillPointsForLevel(
                            progression.Level, config.SkillPointsPerLevel);
                        progression.SkillPoints += skillPoints;

                        byte talentPoints = 0;
                        if (ProgressionHelpers.QualifiesForTalentPoint(progression.Level, config.TalentPointInterval))
                        {
                            talentPoints = config.TalentPointsPerLevel;
                            progression.TalentPoints += talentPoints;
                        }

                        // Emit level up event
                        if (SystemAPI.HasBuffer<LevelUpEvent>(req.TargetEntity))
                        {
                            var levelEvents = SystemAPI.GetBuffer<LevelUpEvent>(req.TargetEntity);
                            levelEvents.Add(new LevelUpEvent
                            {
                                OldLevel = oldLevel,
                                NewLevel = progression.Level,
                                SkillPointsGained = skillPoints,
                                TalentPointsGained = talentPoints,
                                Tick = currentTick
                            });
                        }
                    }

                    SystemAPI.SetComponent(req.TargetEntity, progression);
                }

                ecb.DestroyEntity(entity);
            }
        }

        private byte GetMaxLevel(ref SystemState state, Entity entity)
        {
            if (state.EntityManager.HasComponent<ProgressionConfig>(entity))
            {
                return state.EntityManager.GetComponentData<ProgressionConfig>(entity).MaxLevel;
            }
            return 100; // Default max level
        }

        private ProgressionConfig GetProgressionConfig(ref SystemState state, Entity entity)
        {
            if (state.EntityManager.HasComponent<ProgressionConfig>(entity))
            {
                return state.EntityManager.GetComponentData<ProgressionConfig>(entity);
            }
            return new ProgressionConfig
            {
                XPMultiplier = 1f,
                BaseXPPerLevel = 100,
                LevelXPScaling = 1.5f,
                MaxLevel = 100,
                SkillPointsPerLevel = 1,
                TalentPointsPerLevel = 1,
                TalentPointInterval = 5
            };
        }
    }
}

