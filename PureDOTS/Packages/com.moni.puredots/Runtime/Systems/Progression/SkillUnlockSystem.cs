using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using PureDOTS.Runtime.Progression;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Systems.Progression
{
    /// <summary>
    /// System that processes skill unlock requests and manages skill trees.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(XPAllocationSystem))]
    [BurstCompile]
    public partial struct SkillUnlockSystem : ISystem
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

            // Process skill unlock requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<SkillUnlockRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                
                if (!SystemAPI.HasBuffer<UnlockedSkill>(req.TargetEntity) ||
                    !SystemAPI.HasBuffer<SkillXP>(req.TargetEntity) ||
                    !SystemAPI.HasComponent<CharacterProgression>(req.TargetEntity))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var skills = SystemAPI.GetBuffer<UnlockedSkill>(req.TargetEntity);
                var skillXP = SystemAPI.GetBuffer<SkillXP>(req.TargetEntity);
                var progression = SystemAPI.GetComponent<CharacterProgression>(req.TargetEntity);

                // Check if already unlocked
                if (ProgressionHelpers.HasSkill(skills, req.SkillId))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Check skill point cost (1 point per tier)
                if (progression.SkillPoints < req.Tier)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Check mastery requirement
                var domainSkill = ProgressionHelpers.GetSkillXP(skillXP, req.Domain);
                byte maxTier = ProgressionHelpers.GetMaxSkillTierForMastery(domainSkill.Mastery);
                if (req.Tier > maxTier)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Deduct skill points
                progression.SkillPoints -= req.Tier;
                SystemAPI.SetComponent(req.TargetEntity, progression);

                // Add the skill
                skills.Add(new UnlockedSkill
                {
                    SkillId = req.SkillId,
                    Domain = req.Domain,
                    Tier = req.Tier,
                    Rank = 1,
                    UnlockedTick = currentTick,
                    IsActive = true
                });

                // Emit unlock event
                if (SystemAPI.HasBuffer<SkillUnlockedEvent>(req.TargetEntity))
                {
                    var events = SystemAPI.GetBuffer<SkillUnlockedEvent>(req.TargetEntity);
                    events.Add(new SkillUnlockedEvent
                    {
                        SkillId = req.SkillId,
                        Domain = req.Domain,
                        Tier = req.Tier,
                        Tick = currentTick
                    });
                }

                ecb.DestroyEntity(entity);
            }
        }
    }

    /// <summary>
    /// System that handles automatic skill progression for AI-controlled entities.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SkillUnlockSystem))]
    [BurstCompile]
    public partial struct AutoProgressionSystem : ISystem
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

            // Process entities with preordained paths that aren't player-guided
            foreach (var (path, progression, skillXP, entity) in 
                SystemAPI.Query<RefRO<PreordainedPath>, RefRW<CharacterProgression>, DynamicBuffer<SkillXP>>()
                    .WithEntityAccess())
            {
                if (path.ValueRO.PlayerGuided)
                    continue;

                // Skip if no skill points to spend
                if (progression.ValueRW.SkillPoints == 0)
                    continue;

                // Get primary and secondary domain mastery
                var primarySkill = ProgressionHelpers.GetSkillXP(skillXP, path.ValueRO.PrimaryDomain);
                var secondarySkill = ProgressionHelpers.GetSkillXP(skillXP, path.ValueRO.SecondaryDomain);

                // Determine which domain to invest in based on affinity
                SkillDomain targetDomain;
                SkillMastery targetMastery;
                
                // Use deterministic random based on tick and entity
                uint hash = (uint)(entity.Index + currentTick);
                float roll = (hash % 100) / 100f;

                if (roll < path.ValueRO.PrimaryAffinity)
                {
                    targetDomain = path.ValueRO.PrimaryDomain;
                    targetMastery = primarySkill.Mastery;
                }
                else
                {
                    targetDomain = path.ValueRO.SecondaryDomain;
                    targetMastery = secondarySkill.Mastery;
                }

                // Only auto-unlock skills below threshold tier
                byte maxAutoTier = path.ValueRO.AutoSpecializeThreshold;
                byte availableTier = ProgressionHelpers.GetMaxSkillTierForMastery(targetMastery);
                byte targetTier = availableTier < maxAutoTier ? availableTier : maxAutoTier;

                if (targetTier > 0 && progression.ValueRW.SkillPoints >= targetTier)
                {
                    // Auto-unlock would happen here via event/request
                    // For now, just spend points to simulate progression
                    progression.ValueRW.SkillPoints -= targetTier;
                }
            }
        }
    }
}

