using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spells;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Spells
{
    /// <summary>
    /// Detects spell casts within observation range and grants XP toward spell mastery.
    /// Creates ExtendedSpellMastery entries for observed spells.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(SpellCastingSystem))]
    public partial struct ObservationalLearningSystem : ISystem
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

            // Get spell catalog for validation
            if (!SystemAPI.TryGetSingleton<SpellCatalogRef>(out var spellCatalogRef) ||
                !spellCatalogRef.Blob.IsCreated)
            {
                return;
            }

            ref var spellCatalog = ref spellCatalogRef.Blob.Value;

            // Update lookups
            var observableCasterLookup = SystemAPI.GetComponentLookup<ObservableCaster>(true);
            var translationLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var extendedMasteryLookup = SystemAPI.GetBufferLookup<ExtendedSpellMastery>(true);
            observableCasterLookup.Update(ref state);
            translationLookup.Update(ref state);
            extendedMasteryLookup.Update(ref state);

            // Process observed casts
            new ProcessObservationsJob
            {
                SpellCatalog = spellCatalog,
                ObservableCasterLookup = observableCasterLookup,
                TranslationLookup = translationLookup,
                ExtendedMasteryLookup = extendedMasteryLookup,
                CurrentTick = currentTick
            }.ScheduleParallel();

            // Grant XP from observations
            new GrantObservationXpJob
            {
                CurrentTick = currentTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ProcessObservationsJob : IJobEntity
        {
            [ReadOnly]
            public SpellDefinitionBlob SpellCatalog;

            [ReadOnly]
            public ComponentLookup<ObservableCaster> ObservableCasterLookup;

            [ReadOnly]
            public ComponentLookup<LocalTransform> TranslationLookup;

            [ReadOnly]
            public BufferLookup<ExtendedSpellMastery> ExtendedMasteryLookup;

            public uint CurrentTick;

            void Execute(
                Entity observerEntity,
                in SpellObserver observer,
                in LocalTransform transform,
                ref DynamicBuffer<ObservedSpellCast> observations,
                in DynamicBuffer<SpellCastEvent> castEvents)
            {
                // Process recent spell cast events
                for (int i = 0; i < castEvents.Length; i++)
                {
                    var castEvent = castEvents[i];

                    // Skip if not a successful cast
                    if (castEvent.Result != SpellCastResult.Success)
                    {
                        continue;
                    }

                    // Check if caster is observable
                    if (!ObservableCasterLookup.HasComponent(castEvent.CasterEntity))
                    {
                        continue;
                    }

                    var observableCaster = ObservableCasterLookup[castEvent.CasterEntity];
                    if (!observableCaster.CanBeObserved)
                    {
                        continue;
                    }

                    // Check range
                    if (TranslationLookup.HasComponent(castEvent.CasterEntity))
                    {
                        var casterPos = TranslationLookup[castEvent.CasterEntity].Position;
                        float distance = math.distance(transform.Position, casterPos);

                        if (distance > observer.ObservationRange || distance > observableCaster.VisibilityRange)
                        {
                            continue;
                        }
                    }

                    // Check if we're already observing this spell
                    bool alreadyObserving = false;
                    for (int j = 0; j < observations.Length; j++)
                    {
                        if (observations[j].SpellId.Equals(castEvent.SpellId))
                        {
                            alreadyObserving = true;
                            break;
                        }
                    }

                    if (alreadyObserving)
                    {
                        continue;
                    }

                    // Check max simultaneous observations
                    if (observations.Length >= observer.MaxSimultaneousObserve)
                    {
                        // Remove oldest observation
                        observations.RemoveAt(0);
                    }

                    // Calculate quality factor based on caster's mastery
                    float qualityFactor = 0.5f; // Default
                    if (ExtendedMasteryLookup.HasBuffer(castEvent.CasterEntity))
                    {
                        var casterMastery = ExtendedMasteryLookup[castEvent.CasterEntity];
                        for (int j = 0; j < casterMastery.Length; j++)
                        {
                            if (casterMastery[j].SpellId.Equals(castEvent.SpellId))
                            {
                                // Quality = normalized mastery (0-1)
                                qualityFactor = math.clamp(casterMastery[j].MasteryProgress / 4.0f, 0f, 1f);
                                break;
                            }
                        }
                    }

                    // Add observation
                    observations.Add(new ObservedSpellCast
                    {
                        SpellId = castEvent.SpellId,
                        CasterEntity = castEvent.CasterEntity,
                        ObserveTick = CurrentTick,
                        QualityFactor = qualityFactor,
                        CastPosition = castEvent.TargetPosition
                    });
                }
            }
        }

        [BurstCompile]
        public partial struct GrantObservationXpJob : IJobEntity
        {
            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref DynamicBuffer<ObservedSpellCast> observations,
                ref DynamicBuffer<ExtendedSpellMastery> mastery)
            {
                // Process observations and grant XP
                for (int i = observations.Length - 1; i >= 0; i--)
                {
                    var observation = observations[i];

                    // Find or create mastery entry
                    int masteryIndex = -1;
                    for (int j = 0; j < mastery.Length; j++)
                    {
                        if (mastery[j].SpellId.Equals(observation.SpellId))
                        {
                            masteryIndex = j;
                            break;
                        }
                    }

                    if (masteryIndex < 0)
                    {
                        // Create new mastery entry at 0%
                        mastery.Add(new ExtendedSpellMastery
                        {
                            SpellId = observation.SpellId,
                            MasteryProgress = 0f,
                            ObservationCount = 0,
                            PracticeAttempts = 0,
                            SuccessfulCasts = 0,
                            FailedCasts = 0,
                            Signatures = SpellSignatureFlags.None,
                            HybridWithSpellId = default,
                            LastUpdateTick = CurrentTick
                        });
                        masteryIndex = mastery.Length - 1;
                    }

                    // Grant XP based on quality factor
                    // Base XP per observation = 0.01 (1% toward next milestone)
                    // Quality factor multiplies this
                    float baseXp = 0.01f;
                    float xpGain = baseXp * observation.QualityFactor;

                    var masteryEntry = mastery[masteryIndex];
                    masteryEntry.MasteryProgress += xpGain;
                    masteryEntry.ObservationCount++;
                    masteryEntry.LastUpdateTick = CurrentTick;

                    // Clamp to 4.0 (400%)
                    masteryEntry.MasteryProgress = math.min(masteryEntry.MasteryProgress, 4.0f);

                    mastery[masteryIndex] = masteryEntry;

                    // Remove processed observation
                    observations.RemoveAt(i);
                }
            }
        }
    }
}

