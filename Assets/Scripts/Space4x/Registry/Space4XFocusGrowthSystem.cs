using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Tracks focus usage and awards experience/wisdom.
    /// Entities who use focus more reach greater heights.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFocusAbilitySystem))]
    public partial struct Space4XFocusExperienceSystem : ISystem
    {
        // Experience multipliers by intensity tier
        private const float IdleExpMult = 0f;
        private const float LightExpMult = 0.5f;
        private const float ModerateExpMult = 1.0f;
        private const float IntenseExpMult = 2.0f;
        private const float BreakthroughExpMult = 4.0f;

        // Flow state bonus
        private const float FlowStateBonus = 1.5f;
        private const uint FlowStateThreshold = 20; // Ticks to enter flow state

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEntityFocus>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (focus, growth, tracking, abilities) in
                SystemAPI.Query<RefRO<Space4XEntityFocus>, RefRW<FocusGrowth>, RefRW<FocusUsageTracking>, DynamicBuffer<Space4XActiveFocusAbility>>())
            {
                // Skip if in coma
                if (focus.ValueRO.IsInComa != 0)
                {
                    tracking.ValueRW.ContinuousFocusTicks = 0;
                    tracking.ValueRW.IsInFlowState = 0;
                    continue;
                }

                // Calculate current drain ratio
                float drainRatio = focus.ValueRO.MaxFocus > 0
                    ? focus.ValueRO.TotalDrainRate / focus.ValueRO.BaseRegenRate
                    : 0f;

                // Update intensity tier
                byte newTier = FocusUsageTracking.GetIntensityTier(drainRatio);
                tracking.ValueRW.IntensityTier = newTier;

                // Track peak intensity
                if (drainRatio > (float)growth.ValueRO.PeakIntensityAchieved)
                {
                    growth.ValueRW.PeakIntensityAchieved = (half)drainRatio;
                }

                // Track breakthrough moments
                if (drainRatio >= 0.9f)
                {
                    growth.ValueRW.BreakthroughMoments++;
                }

                // Calculate experience gain based on intensity
                float expMultiplier = newTier switch
                {
                    0 => IdleExpMult,
                    1 => LightExpMult,
                    2 => ModerateExpMult,
                    3 => IntenseExpMult,
                    4 => BreakthroughExpMult,
                    _ => 0f
                };

                // Track continuous focus
                if (abilities.Length > 0)
                {
                    tracking.ValueRW.ContinuousFocusTicks++;
                    tracking.ValueRW.SessionDrainAccumulated += focus.ValueRO.TotalDrainRate;
                    growth.ValueRW.CumulativeFocusTime++;

                    // Track peak drain this session
                    if (focus.ValueRO.TotalDrainRate > tracking.ValueRO.SessionPeakDrainRate)
                    {
                        tracking.ValueRW.SessionPeakDrainRate = focus.ValueRO.TotalDrainRate;
                    }

                    // Check for flow state
                    if (tracking.ValueRO.ContinuousFocusTicks >= FlowStateThreshold && newTier >= 2)
                    {
                        if (tracking.ValueRO.IsInFlowState == 0)
                        {
                            tracking.ValueRW.IsInFlowState = 1;
                            tracking.ValueRW.FlowStateStartTick = currentTick;
                        }
                        expMultiplier *= FlowStateBonus;
                    }
                }
                else
                {
                    // Reset continuous tracking when no abilities active
                    if (tracking.ValueRO.ContinuousFocusTicks > 0)
                    {
                        tracking.ValueRW.ContinuousFocusTicks = 0;
                        tracking.ValueRW.IsInFlowState = 0;
                        tracking.ValueRW.SessionDrainAccumulated = 0;
                        tracking.ValueRW.SessionPeakDrainRate = 0;
                    }
                }

                // Award experience
                if (expMultiplier > 0)
                {
                    float baseExpGain = 0.01f; // Base XP per tick
                    float expGain = baseExpGain * expMultiplier;

                    // Bonus from existing wisdom (compound growth)
                    expGain *= (1f + (float)growth.ValueRO.Wisdom * 0.1f);

                    growth.ValueRW.TotalFocusExperience += expGain;
                    growth.ValueRW.ExperienceToNextLevel += expGain;

                    // Check for level up
                    float expRequired = FocusGrowth.GetExperienceForLevel(growth.ValueRO.GrowthLevel);
                    if (growth.ValueRO.ExperienceToNextLevel >= expRequired)
                    {
                        growth.ValueRW.GrowthLevel++;
                        growth.ValueRW.ExperienceToNextLevel -= expRequired;

                        // Wisdom increases with levels (diminishing returns)
                        float wisdomGain = 0.02f / (1f + growth.ValueRO.GrowthLevel * 0.1f);
                        growth.ValueRW.Wisdom = (half)math.min(1f, (float)growth.ValueRO.Wisdom + wisdomGain);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tracks focus achievements and grants bonus experience.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFocusExperienceSystem))]
    public partial struct Space4XFocusAchievementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FocusGrowth>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (growth, tracking, focus, achievements) in
                SystemAPI.Query<RefRW<FocusGrowth>, RefRO<FocusUsageTracking>, RefRO<Space4XEntityFocus>, DynamicBuffer<FocusAchievement>>())
            {
                // Check for first flow state
                if (tracking.ValueRO.IsInFlowState != 0 && !HasAchievement(achievements, FocusAchievementType.FirstFlowState))
                {
                    AddAchievement(achievements, FocusAchievementType.FirstFlowState, currentTick, 1f);
                    AwardBonusExperience(ref growth.ValueRW, 10f);
                }

                // Check for sustained flow (100+ ticks)
                if (tracking.ValueRO.IsInFlowState != 0)
                {
                    uint flowDuration = currentTick - tracking.ValueRO.FlowStateStartTick;
                    if (flowDuration >= 100 && !HasRecentAchievement(achievements, FocusAchievementType.SustainedFlow, currentTick, 1000))
                    {
                        AddAchievement(achievements, FocusAchievementType.SustainedFlow, currentTick, flowDuration);
                        AwardBonusExperience(ref growth.ValueRW, 25f);
                    }
                }

                // Check for breakthrough moment
                if (tracking.ValueRO.IntensityTier >= 4 && !HasRecentAchievement(achievements, FocusAchievementType.BreakthroughMoment, currentTick, 500))
                {
                    AddAchievement(achievements, FocusAchievementType.BreakthroughMoment, currentTick, tracking.ValueRO.SessionPeakDrainRate);
                    AwardBonusExperience(ref growth.ValueRW, 15f);

                    // Breakthrough moments also boost wisdom directly
                    growth.ValueRW.Wisdom = (half)math.min(1f, (float)growth.ValueRO.Wisdom + 0.005f);
                }

                // Check for exhaustion recovery
                if (focus.ValueRO.ExhaustionLevel > 80 && focus.ValueRO.IsInComa == 0)
                {
                    // Entity pushed to exhaustion but didn't enter coma - builds resilience
                    if (!HasRecentAchievement(achievements, FocusAchievementType.ExhaustionRecovery, currentTick, 2000))
                    {
                        AddAchievement(achievements, FocusAchievementType.ExhaustionRecovery, currentTick, focus.ValueRO.ExhaustionLevel);
                        growth.ValueRW.ExhaustionEvents++;
                        AwardBonusExperience(ref growth.ValueRW, 5f);
                    }
                }
            }

            // Check for multi-ability mastery (3+ abilities)
            foreach (var (growth, abilities, achievements) in
                SystemAPI.Query<RefRW<FocusGrowth>, DynamicBuffer<Space4XActiveFocusAbility>, DynamicBuffer<FocusAchievement>>())
            {
                if (abilities.Length >= 3 && !HasRecentAchievement(achievements, FocusAchievementType.MultiAbilityMastery, currentTick, 500))
                {
                    AddAchievement(achievements, FocusAchievementType.MultiAbilityMastery, currentTick, abilities.Length);
                    AwardBonusExperience(ref growth.ValueRW, 20f);
                }
            }
        }

        private static bool HasAchievement(DynamicBuffer<FocusAchievement> achievements, FocusAchievementType type)
        {
            for (int i = 0; i < achievements.Length; i++)
            {
                if (achievements[i].Type == type)
                    return true;
            }
            return false;
        }

        private static bool HasRecentAchievement(DynamicBuffer<FocusAchievement> achievements, FocusAchievementType type, uint currentTick, uint cooldown)
        {
            for (int i = 0; i < achievements.Length; i++)
            {
                if (achievements[i].Type == type && currentTick - achievements[i].AchievedTick < cooldown)
                    return true;
            }
            return false;
        }

        private static void AddAchievement(DynamicBuffer<FocusAchievement> achievements, FocusAchievementType type, uint tick, float value)
        {
            // Remove oldest if at capacity
            if (achievements.Length >= achievements.Capacity)
            {
                achievements.RemoveAt(0);
            }

            achievements.Add(new FocusAchievement
            {
                Type = type,
                AchievedTick = tick,
                Value = value
            });
        }

        private static void AwardBonusExperience(ref FocusGrowth growth, float amount)
        {
            growth.TotalFocusExperience += amount;
            growth.ExperienceToNextLevel += amount;
        }
    }

    /// <summary>
    /// Applies personality-driven focus usage limits.
    /// Most entities won't naturally use 100% focus unless survival or passion drives them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XFocusAbilitySystem))]
    public partial struct Space4XFocusPersonalitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FocusPersonality>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (personality, context, focus, abilities) in
                SystemAPI.Query<RefRO<FocusPersonality>, RefRO<FocusBehaviorContext>, RefRW<Space4XEntityFocus>, DynamicBuffer<Space4XActiveFocusAbility>>())
            {
                // Calculate natural focus usage ceiling
                bool isThreatened = context.ValueRO.IsThreatened != 0 || (float)context.ValueRO.ThreatSeverity > 0.3f;
                bool hasGoal = context.ValueRO.HasActiveGoal != 0 || (float)context.ValueRO.GoalImportance > 0.5f;

                float naturalLimit = personality.ValueRO.GetNaturalFocusUsageRatio(isThreatened, hasGoal);

                // Life-threatening situations override personality
                if ((float)context.ValueRO.ThreatSeverity > 0.8f)
                {
                    naturalLimit = math.max(naturalLimit, 0.95f);
                }

                // High passion for current task overrides laziness
                if ((float)personality.ValueRO.Passion > 0.8f && hasGoal)
                {
                    naturalLimit = math.max(naturalLimit, 0.85f);
                }

                // Social pressure can push beyond natural limits
                if ((float)context.ValueRO.SocialPressure > 0.5f)
                {
                    naturalLimit += (float)context.ValueRO.SocialPressure * 0.15f;
                }

                // External fatigue reduces willingness
                naturalLimit -= (float)context.ValueRO.ExternalFatigue * 0.2f;

                naturalLimit = math.saturate(naturalLimit);

                // Calculate current focus usage ratio
                float currentUsage = focus.ValueRO.TotalDrainRate / math.max(0.1f, focus.ValueRO.BaseRegenRate * 3f);

                // If over natural limit and not in crisis, gradually reduce
                if (currentUsage > naturalLimit && !isThreatened)
                {
                    // Lazy/comfort-seeking entities will deactivate abilities
                    float overUsage = currentUsage - naturalLimit;
                    float deactivationChance = overUsage * (float)personality.ValueRO.Laziness * 0.1f;

                    // Note: Actual deactivation handled by AI systems based on this signal
                    // This system just tracks the psychological pressure to ease off
                }
            }
        }
    }

    /// <summary>
    /// Updates behavior context based on game state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XFocusBehaviorContextSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FocusBehaviorContext>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Update context from combat/situation systems
            foreach (var (context, engagement) in
                SystemAPI.Query<RefRW<FocusBehaviorContext>, RefRO<Space4XEngagement>>())
            {
                // In combat = threatened
                if (engagement.ValueRO.Phase != EngagementPhase.None)
                {
                    context.ValueRW.IsThreatened = 1;

                    // Threat severity based on damage taken
                    // Would calculate from hull damage etc.
                    context.ValueRW.ThreatSeverity = (half)0.5f;
                }
                else
                {
                    context.ValueRW.IsThreatened = 0;
                    context.ValueRW.ThreatSeverity = (half)0f;
                }
            }

            // Update from active situations
            foreach (var (context, situation) in
                SystemAPI.Query<RefRW<FocusBehaviorContext>, RefRO<SituationState>>())
            {
                if (situation.ValueRO.Phase != SituationPhase.Aftermath && situation.ValueRO.Phase != SituationPhase.Resolved)
                {
                    context.ValueRW.HasActiveGoal = 1;

                    // Crisis situations are threatening
                    if (situation.ValueRO.Type == SituationType.EnergyCrisis ||
                        situation.ValueRO.Type == SituationType.SupplyShortage ||
                        situation.ValueRO.Type == SituationType.FuelDepletion)
                    {
                        context.ValueRW.IsThreatened = 1;
                        context.ValueRW.ThreatSeverity = (half)math.max(
                            (float)context.ValueRO.ThreatSeverity,
                            0.6f + (float)situation.ValueRO.Severity * 0.3f
                        );
                    }
                }
            }

            // Decay recent reward over time
            foreach (var context in SystemAPI.Query<RefRW<FocusBehaviorContext>>())
            {
                if ((float)context.ValueRO.RecentReward > 0)
                {
                    context.ValueRW.RecentReward = (half)math.max(0, (float)context.ValueRO.RecentReward - 0.001f);
                }
            }
        }
    }

    /// <summary>
    /// Applies growth bonuses to focus effectiveness.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFocusModifierSystem))]
    public partial struct Space4XFocusGrowthBonusSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FocusGrowth>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (growth, modifiers, profile) in
                SystemAPI.Query<RefRO<FocusGrowth>, RefRW<Space4XFocusModifiers>, RefRW<OfficerFocusProfile>>())
            {
                float growthBonus = growth.ValueRO.GetGrowthBonus();

                // Growth increases focus experience (learning to use focus better)
                if (growthBonus > 1f)
                {
                    float expBonus = (growthBonus - 1f) * 0.5f;
                    profile.ValueRW.FocusExperience = (half)math.min(1f, (float)profile.ValueRO.FocusExperience + expBonus);
                }

                // Wisdom increases mental resilience
                if ((float)growth.ValueRO.Wisdom > 0)
                {
                    float resilienceBonus = (float)growth.ValueRO.Wisdom * 0.2f;
                    profile.ValueRW.MentalResilience = (half)math.min(1f, (float)profile.ValueRO.MentalResilience + resilienceBonus);
                }

                // Growth amplifies modifier effects
                if (growthBonus > 1f)
                {
                    // Boost key modifiers based on growth
                    float boost = growthBonus - 1f;

                    if ((float)modifiers.ValueRO.AccuracyBonus > 0)
                        modifiers.ValueRW.AccuracyBonus = (half)((float)modifiers.ValueRO.AccuracyBonus * (1f + boost * 0.5f));

                    if ((float)modifiers.ValueRO.DetectionBonus > 0)
                        modifiers.ValueRW.DetectionBonus = (half)((float)modifiers.ValueRO.DetectionBonus * (1f + boost * 0.5f));

                    if ((float)modifiers.ValueRO.RepairSpeedMultiplier > 1f)
                        modifiers.ValueRW.RepairSpeedMultiplier = (half)(1f + ((float)modifiers.ValueRO.RepairSpeedMultiplier - 1f) * (1f + boost * 0.5f));

                    if ((float)modifiers.ValueRO.ProductionSpeedMultiplier > 1f)
                        modifiers.ValueRW.ProductionSpeedMultiplier = (half)(1f + ((float)modifiers.ValueRO.ProductionSpeedMultiplier - 1f) * (1f + boost * 0.5f));
                }
            }
        }
    }

    /// <summary>
    /// Telemetry for focus growth.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XFocusGrowthTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FocusGrowth>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalOfficers = 0;
            float avgLevel = 0;
            float avgWisdom = 0;
            int inFlowState = 0;
            int breakthroughsThisSession = 0;

            foreach (var (growth, tracking) in SystemAPI.Query<RefRO<FocusGrowth>, RefRO<FocusUsageTracking>>())
            {
                totalOfficers++;
                avgLevel += growth.ValueRO.GrowthLevel;
                avgWisdom += (float)growth.ValueRO.Wisdom;

                if (tracking.ValueRO.IsInFlowState != 0)
                    inFlowState++;

                if (tracking.ValueRO.IntensityTier >= 4)
                    breakthroughsThisSession++;
            }

            if (totalOfficers > 0)
            {
                avgLevel /= totalOfficers;
                avgWisdom /= totalOfficers;
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[FocusGrowth] Officers: {totalOfficers}, AvgLevel: {avgLevel:F1}, AvgWisdom: {avgWisdom:P0}, InFlow: {inFlowState}, Breakthroughs: {breakthroughsThisSession}");
        }
    }
}

