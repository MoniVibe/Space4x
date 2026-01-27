using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Spells;
using PureDOTS.Systems.Knowledge;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Spells
{
    /// <summary>
    /// Processes spell learning requests, validates prerequisites (lessons, enlightenment, prior spells),
    /// and adds LearnedSpell to entity's buffer.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(LessonProgressionSystem))]
    public partial struct SpellLearningSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            var enlightenmentLookup = SystemAPI.GetComponentLookup<Enlightenment>(true);

            // Get spell catalog
            if (!SystemAPI.TryGetSingleton<SpellCatalogRef>(out var spellCatalogRef) ||
                !spellCatalogRef.Blob.IsCreated)
            {
                return;
            }

            // Get lesson catalog
            var lessonCatalogRef = SystemAPI.GetSingleton<LessonCatalogRef>();
            if (!lessonCatalogRef.Blob.IsCreated)
            {
                return;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new ProcessSpellLearningJob
            {
                SpellCatalog = spellCatalogRef.Blob,
                LessonCatalog = lessonCatalogRef.Blob,
                CurrentTick = currentTick,
                Ecb = ecb,
                EnlightenmentLookup = enlightenmentLookup
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ProcessSpellLearningJob : IJobEntity
        {
            [ReadOnly]
            public BlobAssetReference<SpellDefinitionBlob> SpellCatalog;

            [ReadOnly]
            public BlobAssetReference<LessonDefinitionBlob> LessonCatalog;

            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<Enlightenment> EnlightenmentLookup;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                ref DynamicBuffer<SpellLearnRequest> requests,
                ref DynamicBuffer<LearnedSpell> learnedSpells,
                in DynamicBuffer<LessonMastery> lessonMastery,
                ref DynamicBuffer<SpellLearnedEvent> learnedEvents)
            {
                ref var spellCatalog = ref SpellCatalog.Value;
                ref var lessonCatalog = ref LessonCatalog.Value;

                for (int i = requests.Length - 1; i >= 0; i--)
                {
                    var request = requests[i];

                    // Find spell definition
                    var spellIndex = -1;
                    for (int j = 0; j < spellCatalog.Spells.Length; j++)
                    {
                        if (spellCatalog.Spells[j].SpellId.Equals(request.SpellId))
                        {
                            spellIndex = j;
                            break;
                        }
                    }

                    if (spellIndex == -1)
                    {
                        requests.RemoveAt(i);
                        continue; // Invalid spell
                    }

                    // Check if already learned
                    bool alreadyLearned = false;
                    for (int j = 0; j < learnedSpells.Length; j++)
                    {
                        if (learnedSpells[j].SpellId.Equals(request.SpellId))
                        {
                            alreadyLearned = true;
                            break;
                        }
                    }

                    if (alreadyLearned)
                    {
                        requests.RemoveAt(i);
                        continue; // Already learned
                    }

                    // Validate prerequisites
                    ref var spellEntry = ref spellCatalog.Spells[spellIndex];
                    if (!ValidateSpellPrerequisites(entity, ref spellEntry, lessonMastery, ref lessonCatalog, learnedSpells))
                    {
                        requests.RemoveAt(i);
                        continue; // Prerequisites not met
                    }

                    // Add learned spell
                    learnedSpells.Add(new LearnedSpell
                    {
                        SpellId = request.SpellId,
                        MasteryLevel = 0, // Start at 0, increases with use
                        TimesCast = 0,
                        LearnedTick = CurrentTick,
                        TeacherEntity = request.TeacherEntity
                    });

                    // Emit event
                    learnedEvents.Add(new SpellLearnedEvent
                    {
                        SpellId = request.SpellId,
                        Entity = entity,
                        TeacherEntity = request.TeacherEntity,
                        LearnedTick = CurrentTick
                    });

                    requests.RemoveAt(i);
                }
            }

            [BurstCompile]
            private bool ValidateSpellPrerequisites(
                Entity entity,
                ref SpellEntry spell,
                DynamicBuffer<LessonMastery> lessonMastery,
                ref LessonDefinitionBlob lessonCatalog,
                in DynamicBuffer<LearnedSpell> learnedSpells)
            {
                // Check enlightenment requirement
                if (EnlightenmentLookup.HasComponent(entity))
                {
                    var enlightenment = EnlightenmentLookup[entity];
                    if (enlightenment.Level < spell.RequiredEnlightenment)
                    {
                        return false;
                    }
                }

                // Check spell prerequisites
                for (int i = 0; i < spell.Prerequisites.Length; i++)
                {
                    var prereq = spell.Prerequisites[i];
                    bool met = false;

                    switch (prereq.Type)
                    {
                        case PrerequisiteType.Lesson:
                            // Check if lesson is mastered to required tier
                            for (int j = 0; j < lessonMastery.Length; j++)
                            {
                                if (lessonMastery[j].LessonId.Equals(prereq.TargetId))
                                {
                                    if (lessonMastery[j].Tier >= (MasteryTier)prereq.RequiredLevel)
                                    {
                                        met = true;
                                        break;
                                    }
                                }
                            }
                            break;

                        case PrerequisiteType.Spell:
                            // Check if spell is learned
                            if (learnedSpells.IsCreated && learnedSpells.Length > 0)
                            {
                                for (int j = 0; j < learnedSpells.Length; j++)
                                {
                                    if (learnedSpells[j].SpellId.Equals(prereq.TargetId))
                                    {
                                        if (learnedSpells[j].MasteryLevel >= prereq.RequiredLevel)
                                        {
                                            met = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            break;

                        case PrerequisiteType.Skill:
                            // TODO: Check skill level
                            met = true; // Placeholder
                            break;

                        case PrerequisiteType.Attribute:
                            // TODO: Check attribute level
                            met = true; // Placeholder
                            break;

                        case PrerequisiteType.Enlightenment:
                            if (EnlightenmentLookup.HasComponent(entity))
                            {
                                var enlightenment = EnlightenmentLookup[entity];
                                if (enlightenment.Level >= prereq.RequiredLevel)
                                {
                                    met = true;
                                }
                            }
                            break;
                    }

                    if (!met)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Request to learn a spell.
    /// </summary>
    public struct SpellLearnRequest : IBufferElementData
    {
        public FixedString64Bytes SpellId;
        public Entity TeacherEntity; // Entity.Null if self-learned
        public uint RequestTick;
    }

    /// <summary>
    /// Event raised when a spell is learned.
    /// </summary>
    public struct SpellLearnedEvent : IBufferElementData
    {
        public FixedString64Bytes SpellId;
        public Entity Entity;
        public Entity TeacherEntity;
        public uint LearnedTick;
    }
}

