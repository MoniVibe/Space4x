using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Skills;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Knowledge
{
    /// <summary>
    /// Attribute types for lesson prerequisites and skill checks.
    /// </summary>
    public enum AttributeType : byte
    {
        Strength,
        Dexterity,
        Constitution,
        Intelligence,
        Wisdom,
        Charisma
    }

    /// <summary>
    /// Processes lesson acquisition requests, validates prerequisites, and adds LessonMastery entries.
    /// Handles event-based triggers (combat, crafting, social), mentor teaching, and book/scroll learning.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial struct LessonAcquisitionSystem : ISystem
    {
        private ComponentLookup<Enlightenment> _enlightenmentLookup;
        private ComponentLookup<SkillSet> _skillSetLookup;
        private ComponentLookup<VillagerAttributes> _attributesLookup;
        private BufferLookup<LessonMastery> _masteryLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _enlightenmentLookup = state.GetComponentLookup<Enlightenment>(true);
            _skillSetLookup = state.GetComponentLookup<SkillSet>(true);
            _attributesLookup = state.GetComponentLookup<VillagerAttributes>(true);
            _masteryLookup = state.GetBufferLookup<LessonMastery>(true);
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

            _enlightenmentLookup.Update(ref state);
            _skillSetLookup.Update(ref state);
            _attributesLookup.Update(ref state);
            _masteryLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new ProcessAcquisitionRequestsJob
            {
                LessonCatalog = lessonCatalogRef.Blob,
                CurrentTick = currentTick,
                Ecb = ecb,
                EnlightenmentLookup = _enlightenmentLookup,
                SkillSetLookup = _skillSetLookup,
                AttributesLookup = _attributesLookup,
                MasteryLookup = _masteryLookup
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ProcessAcquisitionRequestsJob : IJobEntity
        {
            [ReadOnly]
            public BlobAssetReference<LessonDefinitionBlob> LessonCatalog;

            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<Enlightenment> EnlightenmentLookup;
            [ReadOnly] public ComponentLookup<SkillSet> SkillSetLookup;
            [ReadOnly] public ComponentLookup<VillagerAttributes> AttributesLookup;
            [ReadOnly] public BufferLookup<LessonMastery> MasteryLookup;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                ref DynamicBuffer<LessonAcquisitionRequest> requests,
                ref DynamicBuffer<LessonMastery> lessonMastery,
                ref DynamicBuffer<LessonAcquiredEvent> acquiredEvents)
            {
                ref var catalog = ref LessonCatalog.Value;

                for (int i = requests.Length - 1; i >= 0; i--)
                {
                    var request = requests[i];

                    // Find lesson definition
                    int lessonIndex = FindLessonIndex(ref catalog, request.LessonId);
                    if (lessonIndex < 0)
                    {
                        requests.RemoveAt(i);
                        continue; // Invalid lesson
                    }

                    ref var lessonEntry = ref catalog.Lessons[lessonIndex];

                    // Check if already learned
                    if (HasLesson(ref lessonMastery, request.LessonId))
                    {
                        requests.RemoveAt(i);
                        continue; // Already learned
                    }

                    // Validate prerequisites
                    if (!ValidateLessonPrerequisites(entity, ref lessonEntry, ref lessonMastery))
                    {
                        requests.RemoveAt(i);
                        continue; // Prerequisites not met
                    }

                    // Apply source-specific modifiers
                    var startingTier = MasteryTier.Novice;
                    var startingProgress = 0f;

                    switch (request.Source)
                    {
                        case LessonAcquisitionSource.Teaching:
                            // Teaching provides a head start based on teacher's mastery
                            if (request.TeacherEntity != Entity.Null && MasteryLookup.HasBuffer(request.TeacherEntity))
                            {
                                var teacherMastery = MasteryLookup[request.TeacherEntity];
                                var teacherTier = GetMasteryTier(ref teacherMastery, request.LessonId);
                                
                                // Student starts with some progress based on teacher's tier
                                // Higher tier teachers provide better instruction
                                if (teacherTier != MasteryTier.None)
                                {
                                    startingProgress = math.min(0.5f, (float)teacherTier * 0.1f);
                                }
                            }
                            break;

                        case LessonAcquisitionSource.Reading:
                            // Book/scroll learning - slower but consistent
                            startingProgress = 0.1f; // Books provide baseline understanding
                            break;

                        case LessonAcquisitionSource.Observation:
                            // Observing others - small XP boost
                            startingProgress = 0.05f;
                            break;

                        case LessonAcquisitionSource.Experimentation:
                            // Trial and error - variable results
                            startingProgress = 0.15f; // Experimentation can yield insights
                            break;

                        case LessonAcquisitionSource.Failure:
                            // Learning from mistakes - small but valuable
                            startingProgress = 0.02f;
                            break;

                        case LessonAcquisitionSource.Practice:
                        case LessonAcquisitionSource.Discovery:
                        default:
                            // Standard acquisition
                            startingProgress = 0f;
                            break;
                    }

                    // Add lesson mastery
                    lessonMastery.Add(new LessonMastery
                    {
                        LessonId = request.LessonId,
                        Tier = startingTier,
                        TierProgress = startingProgress,
                        Progress = startingProgress,
                        TotalXp = startingProgress * 100f, // Approximate XP
                        LastProgressTick = CurrentTick
                    });

                    // Emit event
                    acquiredEvents.Add(new LessonAcquiredEvent
                    {
                        LessonId = request.LessonId,
                        Entity = entity,
                        TeacherEntity = request.TeacherEntity,
                        Source = request.Source,
                        AcquiredTick = CurrentTick
                    });

                    requests.RemoveAt(i);
                }
            }

            private static int FindLessonIndex(ref LessonDefinitionBlob catalog, FixedString64Bytes lessonId)
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

            private static bool HasLesson(ref DynamicBuffer<LessonMastery> mastery, FixedString64Bytes lessonId)
            {
                for (int j = 0; j < mastery.Length; j++)
                {
                    if (mastery[j].LessonId.Equals(lessonId))
                    {
                        return true;
                    }
                }
                return false;
            }

            private static MasteryTier GetMasteryTier(ref DynamicBuffer<LessonMastery> mastery, FixedString64Bytes lessonId)
            {
                for (int j = 0; j < mastery.Length; j++)
                {
                    if (mastery[j].LessonId.Equals(lessonId))
                    {
                        return mastery[j].Tier;
                    }
                }
                return MasteryTier.None;
            }

            private bool ValidateLessonPrerequisites(Entity entity, ref LessonEntry lesson, ref DynamicBuffer<LessonMastery> lessonMastery)
            {
                // Check enlightenment requirement
                if (lesson.RequiredEnlightenment > 0)
                {
                    if (EnlightenmentLookup.HasComponent(entity))
                    {
                        var enlightenment = EnlightenmentLookup[entity];
                        if (enlightenment.Level < lesson.RequiredEnlightenment)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                // Check prerequisites
                for (int i = 0; i < lesson.Prerequisites.Length; i++)
                {
                    var prereq = lesson.Prerequisites[i];
                    bool met = false;

                    switch (prereq.Type)
                    {
                        case LessonPrerequisiteType.Lesson:
                            // Check if prerequisite lesson is mastered to required tier
                            for (int j = 0; j < lessonMastery.Length; j++)
                            {
                                if (lessonMastery[j].LessonId.Equals(prereq.TargetId) &&
                                    lessonMastery[j].Tier >= prereq.RequiredTier)
                                {
                                    met = true;
                                    break;
                                }
                            }
                            break;

                        case LessonPrerequisiteType.Spell:
                            // Check spell mastery buffer if available
                            // For now, assume met if we can't check
                            met = true;
                            break;

                        case LessonPrerequisiteType.Skill:
                            // Check skill level
                            if (SkillSetLookup.HasComponent(entity))
                            {
                                var skillSet = SkillSetLookup[entity];
                                // Map prereq.TargetId to SkillId and check level
                                // For simplicity, check if any skill meets the level
                                met = skillSet.GetMaxLevel() >= prereq.RequiredLevel;
                            }
                            else
                            {
                                met = prereq.RequiredLevel == 0;
                            }
                            break;

                        case LessonPrerequisiteType.Attribute:
                            // Check attribute level
                            if (AttributesLookup.HasComponent(entity))
                            {
                                var attributes = AttributesLookup[entity];
                                // Check if any attribute meets the requirement
                                met = CheckAttributeRequirement(attributes, prereq.TargetId, prereq.RequiredLevel);
                            }
                            else
                            {
                                met = prereq.RequiredLevel == 0;
                            }
                            break;

                        case LessonPrerequisiteType.Enlightenment:
                            if (EnlightenmentLookup.HasComponent(entity))
                            {
                                var enlightenment = EnlightenmentLookup[entity];
                                met = enlightenment.Level >= prereq.RequiredLevel;
                            }
                            break;

                        case LessonPrerequisiteType.Culture:
                            // Culture checks would require a culture component
                            // For now, assume met
                            met = true;
                            break;
                    }

                    if (!met)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool CheckAttributeRequirement(in VillagerAttributes attributes, in FixedString64Bytes attributeId, byte requiredLevel)
            {
                // Map attribute ID string to enum
                var attributeType = MapAttributeIdToType(attributeId);

                // Map attribute type to actual attribute value
                switch (attributeType)
                {
                    case AttributeType.Strength:
                        return attributes.Strength >= requiredLevel;
                    case AttributeType.Dexterity:
                        return attributes.Agility >= requiredLevel;
                    case AttributeType.Constitution:
                        return attributes.Physique >= requiredLevel;
                    case AttributeType.Intelligence:
                        return attributes.Intelligence >= requiredLevel;
                    case AttributeType.Wisdom:
                        return attributes.Wisdom >= requiredLevel;
                    case AttributeType.Charisma:
                        return attributes.Willpower >= requiredLevel;
                    default:
                        // Unknown attribute - assume met
                        return true;
                }
            }

            private static AttributeType MapAttributeIdToType(FixedString64Bytes attributeId)
            {
                // Compare against string literals directly - Burst handles this efficiently
                if (attributeId == "strength") return AttributeType.Strength;
                if (attributeId == "dexterity") return AttributeType.Dexterity;
                if (attributeId == "constitution") return AttributeType.Constitution;
                if (attributeId == "intelligence") return AttributeType.Intelligence;
                if (attributeId == "wisdom") return AttributeType.Wisdom;
                if (attributeId == "charisma") return AttributeType.Charisma;

                return AttributeType.Strength; // Default fallback
            }
        }
    }
}

