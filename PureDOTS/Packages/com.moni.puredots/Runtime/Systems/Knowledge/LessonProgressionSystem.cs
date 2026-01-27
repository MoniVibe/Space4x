using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Knowledge
{
    /// <summary>
    /// Processes lesson XP gain, updates mastery progress, and promotes tiers when thresholds are reached.
    /// Emits MasteryAchievedEvent when tier increases.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(LessonAcquisitionSystem))]
    public partial struct LessonProgressionSystem : ISystem
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

            // Get lesson catalog
            if (!SystemAPI.TryGetSingleton<LessonCatalogRef>(out var lessonCatalogRef) ||
                !lessonCatalogRef.Blob.IsCreated)
            {
                return;
            }

            ref var lessonCatalog = ref lessonCatalogRef.Blob.Value;

            new ProcessLessonProgressionJob
            {
                LessonCatalog = lessonCatalog,
                CurrentTick = currentTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ProcessLessonProgressionJob : IJobEntity
        {
            [ReadOnly]
            public LessonDefinitionBlob LessonCatalog;

            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref DynamicBuffer<LessonXpGain> xpGains,
                ref DynamicBuffer<LessonMastery> lessonMastery,
                ref DynamicBuffer<MasteryAchievedEvent> masteryEvents)
            {
                // Process XP gains
                for (int i = xpGains.Length - 1; i >= 0; i--)
                {
                    var xpGain = xpGains[i];

                    // Find matching lesson mastery
                    int masteryIndex = -1;
                    for (int j = 0; j < lessonMastery.Length; j++)
                    {
                        if (lessonMastery[j].LessonId.Equals(xpGain.LessonId))
                        {
                            masteryIndex = j;
                            break;
                        }
                    }

                    if (masteryIndex < 0)
                    {
                        // Lesson not learned yet, skip
                        xpGains.RemoveAt(i);
                        continue;
                    }

                    // Find lesson definition for XP per tier
                    float xpPerTier = 100f; // Default
                    for (int j = 0; j < LessonCatalog.Lessons.Length; j++)
                    {
                        if (LessonCatalog.Lessons[j].LessonId.Equals(xpGain.LessonId))
                        {
                            xpPerTier = LessonCatalog.Lessons[j].XpPerTier;
                            break;
                        }
                    }

                    // Apply XP gain
                    var mastery = lessonMastery[masteryIndex];
                    mastery.TotalXp += xpGain.XpAmount;
                    mastery.LastProgressTick = CurrentTick;

                    // Calculate current tier and progress
                    MasteryTier oldTier = mastery.Tier;
                    float totalProgress = CalculateTotalProgress(mastery.TotalXp, xpPerTier);
                    mastery.Tier = MasteryTierUtility.GetTierFromProgress(totalProgress);
                    mastery.TierProgress = MasteryTierUtility.GetProgressWithinTier(totalProgress);
                    mastery.Progress = mastery.TierProgress;

                    // Check for tier up
                    if (mastery.Tier > oldTier)
                    {
                        masteryEvents.Add(new MasteryAchievedEvent
                        {
                            LessonId = mastery.LessonId,
                            NewTier = mastery.Tier,
                            PreviousTier = oldTier,
                            AchievedTick = CurrentTick
                        });
                    }

                    lessonMastery[masteryIndex] = mastery;
                    xpGains.RemoveAt(i);
                }
            }

            [BurstCompile]
            private float CalculateTotalProgress(float totalXp, float xpPerTier)
            {
                // Progress = (TotalXP / XPPerTier) / 5 (5 tiers: Novice->Apprentice->Journeyman->Expert->Master->Grandmaster)
                float tiersCompleted = totalXp / xpPerTier;
                return math.clamp(tiersCompleted / 5f, 0f, 1f);
            }
        }
    }
}

