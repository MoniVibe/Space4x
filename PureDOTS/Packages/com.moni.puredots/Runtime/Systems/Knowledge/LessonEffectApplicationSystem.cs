using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Knowledge;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Knowledge
{
    /// <summary>
    /// Aggregates lesson effects into LessonEffectCache component.
    /// Runs after LessonProgressionSystem to update bonuses when mastery changes.
    /// Handles prerequisites, mutually exclusive lessons, decay, and spell unlocks.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(LessonProgressionSystem))]
    public partial struct LessonEffectApplicationSystem : ISystem
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
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var deltaTime = timeState.FixedDeltaTime;

            // Get lesson catalog
            if (!SystemAPI.TryGetSingleton<LessonCatalogRef>(out var lessonCatalogRef) ||
                !lessonCatalogRef.Blob.IsCreated)
            {
                return;
            }

            // Get decay config (optional)
            var decayConfig = SystemAPI.HasSingleton<LessonDecayConfig>()
                ? SystemAPI.GetSingleton<LessonDecayConfig>()
                : LessonDecayConfig.CreateDefaults();

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new AggregateLessonEffectsJob
            {
                LessonCatalog = lessonCatalogRef.Blob,
                CurrentTick = currentTick,
                DeltaTime = deltaTime,
                DecayConfig = decayConfig,
                Ecb = ecb
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct AggregateLessonEffectsJob : IJobEntity
        {
            [ReadOnly]
            public BlobAssetReference<LessonDefinitionBlob> LessonCatalog;

            public uint CurrentTick;
            public float DeltaTime;
            public LessonDecayConfig DecayConfig;
            public EntityCommandBuffer.ParallelWriter Ecb;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                ref DynamicBuffer<LessonMastery> lessonMastery,
                ref LessonEffectCache effectCache)
            {
                ref var catalog = ref LessonCatalog.Value;

                // Reset cache
                effectCache = new LessonEffectCache
                {
                    HarvestYieldMultiplier = 1f,
                    HarvestTimeMultiplier = 1f,
                    HarvestQualityBonus = 0f,
                    CraftingQualityBonus = 0f,
                    CraftingSpeedMultiplier = 1f,
                    CraftingEfficiencyBonus = 0f,
                    CombatDamageBonus = 0f,
                    CombatAccuracyBonus = 0f,
                    CombatDefenseBonus = 0f,
                    GeneralSkillBonus = 0f,
                    UnlockedSpellFlags = 0,
                    LastUpdateTick = CurrentTick
                };

                // First pass: Apply decay and check prerequisites
                for (int i = lessonMastery.Length - 1; i >= 0; i--)
                {
                    var mastery = lessonMastery[i];
                    if (mastery.Progress == 0f && mastery.TierProgress > 0f)
                    {
                        mastery.Progress = mastery.TierProgress;
                    }

                    // Find lesson definition
                    int lessonIndex = FindLessonIndex(ref catalog, mastery.LessonId);
                    if (lessonIndex < 0)
                    {
                        continue;
                    }

                    ref var lessonEntry = ref catalog.Lessons[lessonIndex];

                    // Check prerequisites
                    if (!CheckPrerequisites(ref catalog, ref lessonEntry, ref lessonMastery))
                    {
                        // Prerequisites not met - apply penalty or skip effects
                        // Don't remove the lesson, just don't apply its effects
                        continue;
                    }

                    // Check for mutually exclusive lessons (opposites)
                    if (HasMutuallyExclusiveLesson(ref catalog, ref lessonEntry, ref lessonMastery, out var exclusiveIndex))
                    {
                        // Both lessons exist - apply reduced effects based on relative mastery
                        var exclusiveMastery = lessonMastery[exclusiveIndex];
                        var masteryRatio = (float)mastery.Tier / math.max(1f, (float)mastery.Tier + (float)exclusiveMastery.Tier);
                        
                        // Apply effects with reduced multiplier
                        ApplyLessonEffects(ref lessonEntry, mastery.Tier, masteryRatio, ref effectCache);
                    }
                    else
                    {
                        // No conflict - apply full effects
                        ApplyLessonEffects(ref lessonEntry, mastery.Tier, 1f, ref effectCache);
                    }

                    // Apply decay over time (only if decay is enabled)
                    if (DecayConfig.DecayEnabled && mastery.Tier != MasteryTier.None)
                    {
                        // Decay is slower for higher tiers
                        var tierDecayMultiplier = 1f / (1f + (float)mastery.Tier * 0.5f);
                        var decayAmount = DecayConfig.BaseDecayRate * tierDecayMultiplier * DeltaTime;
                        
                        mastery.Progress = math.clamp(mastery.Progress - decayAmount, 0f, 1f);
                        
                        // Check for tier demotion
                        if (mastery.Progress <= 0f && mastery.Tier > MasteryTier.Novice)
                        {
                            mastery.Tier = (MasteryTier)((int)mastery.Tier - 1);
                            mastery.Progress = 0.9f; // Start near top of previous tier
                        }

                        mastery.TierProgress = mastery.Progress;
                        lessonMastery[i] = mastery;
                    }
                }
            }

            private int FindLessonIndex(ref LessonDefinitionBlob catalog, FixedString64Bytes lessonId)
            {
                for (int j = 0; j < catalog.Lessons.Length; j++)
                {
                    if (catalog.Lessons[j].LessonId.Equals(lessonId))
                    {
                        return j;
                    }
                }
                return -1;
            }

            private bool CheckPrerequisites(ref LessonDefinitionBlob catalog, ref LessonEntry lessonEntry, ref DynamicBuffer<LessonMastery> lessonMastery)
            {
                for (int p = 0; p < lessonEntry.Prerequisites.Length; p++)
                {
                    var prereq = lessonEntry.Prerequisites[p];
                    
                    if (prereq.Type == LessonPrerequisiteType.Lesson)
                    {
                        // Check if prerequisite lesson is learned at required tier
                        bool found = false;
                        for (int m = 0; m < lessonMastery.Length; m++)
                        {
                            if (lessonMastery[m].LessonId.Equals(prereq.TargetId) && 
                                lessonMastery[m].Tier >= prereq.RequiredTier)
                            {
                                found = true;
                                break;
                            }
                        }
                        
                        if (!found)
                        {
                            return false;
                        }
                    }
                    // Other prerequisite types (Skill, Attribute, etc.) would need additional lookups
                }
                
                return true;
            }

            private bool HasMutuallyExclusiveLesson(ref LessonDefinitionBlob catalog, ref LessonEntry lessonEntry, ref DynamicBuffer<LessonMastery> lessonMastery, out int exclusiveIndex)
            {
                exclusiveIndex = -1;
                
                // Check for lessons that are mutually exclusive (opposing schools, etc.)
                // This is determined by looking for lessons with "opposite_" prefix or specific exclusion markers
                // For now, we check if the lesson category has a known opposite
                
                for (int m = 0; m < lessonMastery.Length; m++)
                {
                    var otherMastery = lessonMastery[m];
                    if (otherMastery.LessonId.Equals(lessonEntry.LessonId))
                    {
                        continue;
                    }
                    
                    int otherIndex = FindLessonIndex(ref catalog, otherMastery.LessonId);
                    if (otherIndex < 0)
                    {
                        continue;
                    }
                    
                    ref var otherEntry = ref catalog.Lessons[otherIndex];
                    
                    // Check if lessons are mutually exclusive based on category combinations
                    // Light vs Shadow, Order vs Chaos, etc.
                    if (AreMutuallyExclusive(lessonEntry.Category, otherEntry.Category))
                    {
                        exclusiveIndex = m;
                        return true;
                    }
                }
                
                return false;
            }

            private static bool AreMutuallyExclusive(LessonCategory a, LessonCategory b)
            {
                // Define mutually exclusive category pairs
                // This is a simplified implementation - games may want more nuanced rules
                return false; // By default, no lessons are mutually exclusive
            }

            private void ApplyLessonEffects(ref LessonEntry lessonEntry, MasteryTier currentTier, float effectMultiplier, ref LessonEffectCache cache)
            {
                for (int j = 0; j < lessonEntry.Effects.Length; j++)
                {
                    var effect = lessonEntry.Effects[j];
                    if (currentTier >= effect.RequiredTier)
                    {
                        ApplyEffect(effect, effectMultiplier, ref cache);
                    }
                }
            }

            [BurstCompile]
            private void ApplyEffect(LessonEffect effect, float multiplier, ref LessonEffectCache cache)
            {
                var scaledValue = effect.Value * multiplier;
                
                switch (effect.Type)
                {
                    case LessonEffectType.YieldMultiplier:
                        cache.HarvestYieldMultiplier *= (1f + scaledValue);
                        break;

                    case LessonEffectType.QualityBonus:
                        cache.HarvestQualityBonus += scaledValue;
                        cache.CraftingQualityBonus += scaledValue;
                        break;

                    case LessonEffectType.SpeedBonus:
                        cache.HarvestTimeMultiplier *= (1f - scaledValue); // Reduction
                        cache.CraftingSpeedMultiplier *= (1f - scaledValue);
                        break;

                    case LessonEffectType.UnlockSpell:
                        // Set spell unlock flag based on TargetId hash
                        // Each spell gets a bit position based on its ID
                        var spellBit = GetSpellBitFromId(effect.TargetId);
                        cache.UnlockedSpellFlags |= spellBit;
                        break;

                    case LessonEffectType.UnlockRecipe:
                        // Recipe unlocks are tracked separately in a recipe unlock buffer
                        // For now, we just note that recipes can be unlocked
                        break;

                    case LessonEffectType.StatBonus:
                        // Apply stat bonus based on TargetId
                        ApplyStatBonus(effect.TargetId, scaledValue, ref cache);
                        break;

                    case LessonEffectType.SkillBonus:
                        cache.GeneralSkillBonus += scaledValue;
                        break;

                    case LessonEffectType.ResistanceBonus:
                        // Resistance bonuses would go to a separate resistance cache
                        // For now, we apply a small defense bonus
                        cache.CombatDefenseBonus += scaledValue * 0.5f;
                        break;

                    case LessonEffectType.HarvestTimeReduction:
                        cache.HarvestTimeMultiplier *= (1f - scaledValue);
                        break;

                    case LessonEffectType.CraftingEfficiency:
                        cache.CraftingEfficiencyBonus += scaledValue;
                        break;
                }
            }

            private static uint GetSpellBitFromId(FixedString64Bytes spellId)
            {
                // Simple hash to get a bit position (0-31) from spell ID
                if (spellId.Length == 0)
                {
                    return 0;
                }
                
                uint hash = 0;
                for (int i = 0; i < spellId.Length; i++)
                {
                    hash = hash * 31 + spellId[i];
                }
                
                return 1u << (int)(hash % 32);
            }

            private static void ApplyStatBonus(FixedString64Bytes statId, float value, ref LessonEffectCache cache)
            {
                // Map stat IDs to cache fields
                if (statId.Equals((FixedString64Bytes)"damage"))
                {
                    cache.CombatDamageBonus += value;
                }
                else if (statId.Equals((FixedString64Bytes)"accuracy"))
                {
                    cache.CombatAccuracyBonus += value;
                }
                else if (statId.Equals((FixedString64Bytes)"defense"))
                {
                    cache.CombatDefenseBonus += value;
                }
                else if (statId.Equals((FixedString64Bytes)"skill"))
                {
                    cache.GeneralSkillBonus += value;
                }
            }
        }
    }

    /// <summary>
    /// Configuration for lesson decay over time.
    /// </summary>
    public struct LessonDecayConfig : IComponentData
    {
        /// <summary>
        /// Whether decay is enabled.
        /// </summary>
        public bool DecayEnabled;

        /// <summary>
        /// Base decay rate per second (before tier modifiers).
        /// </summary>
        public float BaseDecayRate;

        /// <summary>
        /// Minimum time between uses before decay starts (in seconds).
        /// </summary>
        public float DecayGracePeriod;

        public static LessonDecayConfig CreateDefaults()
        {
            return new LessonDecayConfig
            {
                DecayEnabled = false, // Decay disabled by default
                BaseDecayRate = 0.001f, // Very slow decay
                DecayGracePeriod = 3600f // 1 hour grace period
            };
        }
    }
}

