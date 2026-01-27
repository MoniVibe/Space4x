using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Rendering;
using PureDOTS.Runtime.Skills;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Villagers;
using PureDOTS.Systems;
using PureDOTS.Systems.Villagers;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class VillagerAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        public int villagerId = -1;
        public int factionId;

        [Header("Initial Needs")]
        [Range(0f, 100f)] public float initialHealth = 100f;
        [Range(0f, 100f)] public float maxHealth = 100f;
        [Range(0f, 100f)] public float initialHunger = 20f;
        [Range(0f, 100f)] public float initialEnergy = 80f;
        [Range(0f, 100f)] public float initialMorale = 75f;

        [Header("Resource Pools")]
        [Range(0f, 500f)] public float maxMana = 10f;
        [Range(0f, 500f)] public float initialMana = 10f;

        [Header("Movement & Senses")]
        [Range(0.1f, 10f)] public float baseSpeed = 3f;
        [Range(1f, 80f)] public float visionLitRange = 50f;
        [Range(1f, 80f)] public float visionDimRange = 30f;
        [Range(0.5f, 80f)] public float visionObscuredRange = 10f;
        [Range(0.5f, 60f)] public float hearingQuietRange = 20f;
        [Range(0.5f, 60f)] public float hearingNoisyRange = 10f;
        [Range(0.5f, 60f)] public float hearingCrowdedRange = 3f;

        [Header("Job")]
        public VillagerJob.JobType initialJob = VillagerJob.JobType.None;
        public GameObject initialWorksite;

        [Header("Attributes - Primary (0-10)")]
        [Range(0f, 10f)] public float physique = 5f;
        [Range(0f, 10f)] public float finesse = 5f;
        [Range(0f, 10f)] public float willpower = 5f;

        [Header("Attributes - Derived Bonuses (max 200)")]
        [Range(0f, 190f)] public float strengthBonus = 5f;
        [Range(0f, 190f)] public float agilityBonus = 5f;
        [Range(0f, 190f)] public float intelligenceBonus = 5f;
        [Range(0f, 190f)] public float wisdomBonus = 5f;

        [Header("Discipline & Mood")]
        public VillagerDisciplineType initialDiscipline = VillagerDisciplineType.Unassigned;
        [Range(0, 10)] public byte initialDisciplineLevel;
        [Range(0f, 100f)] public float initialMood = 50f;
        [Range(0.1f, 5f)] public float moodChangeRate = 1f;
        public bool startAvailableForJobs = true;

        [Header("Comfort & Environment")]
        public float preferredTemperature = 20f;
        public float coldTolerance = -15f;
        public float heatTolerance = 35f;

        [Header("Belief & Reputation")]
        public string primaryDeityId = "divine.hand";
        [Range(0f, 1f)] public float faith = 0.5f;
        [Range(0f, 1f)] public float worshipProgress = 0f;
        public float fame;
        public float infamy;
        public float honor;
        public float glory;
        public float renown;
        public float reputation;

        [Header("Combat (Optional)")]
        public bool isCombatCapable;
        [Range(0f, 100f)] public float attackDamage = 5f;
        [Range(0.1f, 5f)] public float attackSpeed = 1f;
        [Range(0f, 100f)] public float defenseRating = 10f;
        [Range(0f, 10f)] public float attackRange = 2f;

        [Header("LOD & Rendering")]
        public bool enableLOD = true;
        [Range(50f, 500f)] public float cullDistance = 200f;
        [Range(0f, 1f)] public float importanceScore = 0.5f;

        [Header("Knowledge & Lessons")]
        public bool knowsLegendaryHarvest;
        public bool knowsRelicHarvest;
        [Tooltip("Additional lesson identifiers (e.g., lesson.harvest.ironoak)")]
        public string[] lessonIds;
    }

    public sealed class VillagerBaker : Baker<VillagerAuthoring>
    {
        public override void Bake(VillagerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            AddComponent(entity, new VillagerId
            {
                Value = authoring.villagerId >= 0 ? authoring.villagerId : authoring.gameObject.GetInstanceID(),
                FactionId = authoring.factionId
            });

            var needs = new VillagerNeeds
            {
                Health = authoring.initialHealth,
                MaxHealth = authoring.maxHealth,
                Temperature = math.clamp(authoring.preferredTemperature, -100f, 100f)
            };
            needs.SetHunger(authoring.initialHunger);
            needs.SetEnergy(authoring.initialEnergy);
            needs.SetMorale(authoring.initialMorale);
            AddComponent(entity, needs);

            var maxMana = math.max(0f, authoring.maxMana);
            var currentMana = math.clamp(authoring.initialMana, 0f, maxMana > 0f ? maxMana : authoring.initialMana);
            AddComponent(entity, new VillagerMana
            {
                MaxMana = maxMana,
                CurrentMana = currentMana
            });

            float ClampPrimary(float value) => math.clamp(value, 0f, 10f);
            float ClampDerived(float value) => math.clamp(value, 0f, 200f);

            var physique = ClampPrimary(authoring.physique);
            var finesse = ClampPrimary(authoring.finesse);
            var willpower = ClampPrimary(authoring.willpower);
            const float derivedBaseline = 10f;

            AddComponent(entity, new VillagerAttributes
            {
                Physique = physique,
                Finesse = finesse,
                Willpower = willpower,
                Strength = ClampDerived(derivedBaseline + physique + authoring.strengthBonus),
                Agility = ClampDerived(derivedBaseline + finesse + authoring.agilityBonus),
                Intelligence = ClampDerived(derivedBaseline + willpower + authoring.intelligenceBonus),
                Wisdom = ClampDerived(derivedBaseline + willpower + authoring.wisdomBonus)
            });

            AddComponent(entity, new VillagerTemperatureProfile
            {
                PreferredTemperature = authoring.preferredTemperature,
                ColdTolerance = authoring.coldTolerance,
                HeatTolerance = authoring.heatTolerance
            });

            var deityId = new FixedString64Bytes(string.IsNullOrWhiteSpace(authoring.primaryDeityId) ? "none" : authoring.primaryDeityId.Trim());
            AddComponent(entity, new VillagerBelief
            {
                PrimaryDeityId = deityId,
                Faith = math.clamp(authoring.faith, 0f, 1f),
                WorshipProgress = math.clamp(authoring.worshipProgress, 0f, 1f)
            });

            AddComponent(entity, new VillagerReputation
            {
                Fame = authoring.fame,
                Infamy = authoring.infamy,
                Honor = authoring.honor,
                Glory = authoring.glory,
                Renown = authoring.renown,
                Reputation = authoring.reputation
            });

            AddComponent(entity, new VillagerAIState
            {
                CurrentState = VillagerAIState.State.Idle,
                CurrentGoal = VillagerAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });

            // Add EntityIntent component (defaults to Idle)
            AddComponent(entity, new EntityIntent
            {
                Mode = IntentMode.Idle,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                TriggeringInterrupt = InterruptType.None,
                IntentSetTick = 0,
                Priority = InterruptPriority.Low,
                IsValid = 0
            });

            // Add Interrupt buffer for interrupt-driven intent system
            AddBuffer<Interrupt>(entity);

            AddComponent(entity, new VillagerJob
            {
                Type = authoring.initialJob,
                Phase = VillagerJob.JobPhase.Idle,
                ActiveTicketId = 0,
                Productivity = 1f,
                LastStateChangeTick = 0
            });

            AddComponent(entity, new VillagerJobTicket
            {
                TicketId = 0,
                JobType = authoring.initialJob,
                ResourceTypeIndex = ushort.MaxValue,
                ResourceEntity = Entity.Null,
                StorehouseEntity = Entity.Null,
                Priority = 0,
                Phase = (byte)VillagerJob.JobPhase.Idle,
                ReservedUnits = 0f,
                AssignedTick = 0,
                LastProgressTick = 0
            });

            AddComponent(entity, new VillagerJobProgress
            {
                Gathered = 0f,
                Delivered = 0f,
                TimeInPhase = 0f,
                LastUpdateTick = 0
            });

            AddComponent(entity, new SkillSet());

            uint knowledgeFlags = 0;
            if (authoring.knowsLegendaryHarvest)
            {
                knowledgeFlags |= VillagerKnowledgeFlags.HarvestLegendary;
            }
            if (authoring.knowsRelicHarvest)
            {
                knowledgeFlags |= VillagerKnowledgeFlags.HarvestRelic;
            }

            var knowledge = new VillagerKnowledge
            {
                Flags = knowledgeFlags
            };

            if (authoring.knowsLegendaryHarvest)
            {
                knowledge.TryAddLesson(ToLessonId("lesson.harvest.legendary"));
            }
            if (authoring.knowsRelicHarvest)
            {
                knowledge.TryAddLesson(ToLessonId("lesson.harvest.relic"));
            }

            if (authoring.lessonIds != null)
            {
                for (int i = 0; i < authoring.lessonIds.Length; i++)
                {
                    knowledge.TryAddLesson(ToLessonId(authoring.lessonIds[i]));
                }
            }

            AddComponent(entity, knowledge);

            AddComponent<SpatialIndexedTag>(entity);

            AddComponent(entity, new VillagerDisciplineState
            {
                Value = authoring.initialDiscipline,
                Level = authoring.initialDisciplineLevel,
                Experience = 0f
            });

            AddComponent(entity, new VillagerMood
            {
                Mood = authoring.initialMood,
                TargetMood = authoring.initialMood,
                MoodChangeRate = authoring.moodChangeRate,
                Wellbeing = authoring.initialMorale
            });

            AddComponent(entity, new VillagerAvailability
            {
                IsAvailable = authoring.startAvailableForJobs ? (byte)1 : (byte)0,
                IsReserved = 0,
                LastChangeTick = 0,
                BusyTime = 0f
            });

            AddComponent(entity, new VillagerMovement
            {
                Velocity = float3.zero,
                DesiredVelocity = float3.zero,
                BaseSpeed = authoring.baseSpeed,
                CurrentSpeed = authoring.baseSpeed,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                IsStuck = 0,
                LastMoveTick = 0
            });

            AddComponent(entity, new VillagerSensors
            {
                VisionRange = authoring.visionLitRange,
                HearingRange = authoring.hearingQuietRange,
                NearestThreat = Entity.Null,
                NearestFood = Entity.Null,
                NearestShelter = Entity.Null,
                LastKnownThreatPosition = float3.zero,
                LastSensorUpdateTick = 0
            });

            AddComponent(entity, new VillagerSensorProfile
            {
                VisionLitRange = authoring.visionLitRange,
                VisionDimRange = authoring.visionDimRange,
                VisionObscuredRange = authoring.visionObscuredRange,
                HearingQuietRange = authoring.hearingQuietRange,
                HearingNoisyRange = authoring.hearingNoisyRange,
                HearingCrowdedRange = authoring.hearingCrowdedRange
            });

            if (authoring.isCombatCapable)
            {
                AddComponent(entity, new VillagerCombatStats
                {
                    AttackDamage = authoring.attackDamage,
                    AttackSpeed = authoring.attackSpeed,
                    CurrentTarget = Entity.Null
                });
            }

            AddComponent(entity, new VillagerAnimationState
            {
                CurrentAnimation = VillagerAnimationState.AnimationType.Idle,
                AnimationSpeed = 1f,
                AnimationTime = 0f,
                ShouldUpdateAnimation = 1
            });

            AddComponent(entity, new VillagerStats
            {
                BirthTick = 0,
                DeathTick = 0,
                TotalWorkDone = 0f,
                TotalResourcesGathered = 0f,
                EnemiesKilled = 0,
                BuildingsConstructed = 0,
                DistanceTraveled = 0f
            });

            AddBuffer<VillagerCommand>(entity);
            AddBuffer<VillagerInventoryItem>(entity);
            AddBuffer<VillagerJobCarryItem>(entity);
            AddBuffer<VillagerRelationship>(entity);
            AddBuffer<VillagerPathWaypoint>(entity);
            AddBuffer<VillagerMemoryEvent>(entity);
            AddBuffer<VillagerHistorySample>(entity);
            AddBuffer<VillagerJobHistorySample>(entity);
            AddBuffer<VillagerLessonShare>(entity);
            AddBuffer<VillagerAggregateLessonTracker>(entity);

            // Add VillagerFlags for SoA optimization (replaces legacy tags)
            AddComponent(entity, new VillagerFlags());
            AddComponent(entity, new VillagerLessonShareState());
            
            // Add WorkOffer/WorkClaim system components
            AddComponent(entity, new WorkClaim());
            AddComponent(entity, new VillagerSeed { Value = (uint)(entity.Index ^ 0x12345678) });
            AddComponent(entity, new VillagerNeedsHot());
            AddComponent(entity, new VillagerShiftState 
            { 
                DayShiftEnabled = 1, 
                NightShiftEnabled = 0,
                IsDaytime = 1,
                ShouldWork = 1,
                LastUpdateTick = 0
            });
            AddComponent(entity, new VillagerJobPriorityState());
            
            // Add spatial layer tag (default to ground layer)
            AddComponent(entity, new SpatialLayerTag { LayerId = 0 });

            AddComponent<RewindableTag>(entity);
            
            // Add RewindImportance for rewind tier classification
            AddComponent(entity, new RewindImportance
            {
                Tier = RewindTier.SnapshotFull
            });
            
            // Add HistoryProfile for rewind proof-of-concept
            // Configured for LocalTransform + Health with TickInterval = 2-3, HorizonTicks = ~300 (5 seconds at 60fps)
            AddComponent(entity, new HistoryProfile
            {
                ProfileId = new FixedString32Bytes("villager_rewind_poc"),
                SamplingFrequencyTicks = 2, // Sample every 2 ticks
                HorizonTicks = 300, // ~5 seconds at 60fps
                RecordFlags = HistoryRecordFlags.Transform | HistoryRecordFlags.Health,
                Priority = 150,
                IsEnabled = true,
                LastSampleTick = 0
            });
            
            // Ensure ComponentHistory buffer exists for LocalTransform
            // The TimeHistoryRecordSystem will populate this buffer
            AddBuffer<ComponentHistory<LocalTransform>>(entity);
            AddComponent(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.Critical,
                OverrideStrideSeconds = 0f
            });

            // Add LOD components for performance scaling
            if (authoring.enableLOD)
            {
                AddComponent(entity, new RenderLODData
                {
                    CameraDistance = 0f,
                    ImportanceScore = authoring.importanceScore,
                    RecommendedLOD = 0,
                    LastUpdateTick = 0
                });

                AddComponent(entity, new RenderCullable
                {
                    CullDistance = authoring.cullDistance,
                    Priority = 128 // Medium priority for villagers
                });

                // Sample index for density control
                var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, 100);
                AddComponent(entity, new RenderSampleIndex
                {
                    SampleIndex = sampleIndex,
                    SampleModulus = 100,
                    ShouldRender = 1
                });
            }

            // Add optimized belief component alongside legacy one
            var deityIndex = (byte)(deityId.GetHashCode() % 256);
            AddComponent(entity, new VillagerBeliefOptimized
            {
                PrimaryDeityIndex = deityIndex,
                Faith = (byte)(authoring.faith * 255f),
                WorshipProgress = (byte)(authoring.worshipProgress * 255f),
                Flags = VillagerBeliefFlags.None,
                LastUpdateTick = 0
            });
        }

        private static FixedString64Bytes ToLessonId(string value)
        {
            FixedString64Bytes str = default;
            if (!string.IsNullOrWhiteSpace(value))
            {
                str = new FixedString64Bytes(value.Trim());
            }

            return str;
        }
    }

    [DisallowMultipleComponent]
    public sealed class VillagerSpawnerAuthoring : MonoBehaviour
    {
        public GameObject villagerPrefab;
        public int initialPopulation = 4;
        public float spawnRadius = 10f;
        public int maxPopulation = 50;
        public float reproductionRate = 0.01f;
    }

    public sealed class VillagerSpawnerBaker : Baker<VillagerSpawnerAuthoring>
    {
        public override void Bake(VillagerSpawnerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            Entity prefabEntity = authoring.villagerPrefab != null
                ? GetEntity(authoring.villagerPrefab, TransformUsageFlags.Dynamic)
                : Entity.Null;

            if (prefabEntity == Entity.Null)
            {
                Debug.LogWarning("VillagerSpawnerAuthoring requires a villager prefab reference.", authoring);
            }

            AddComponent(entity, new VillagerSpawnConfig
            {
                VillagerPrefab = prefabEntity,
                SpawnPosition = authoring.transform.position,
                InitialPopulation = authoring.initialPopulation,
                SpawnRadius = authoring.spawnRadius,
                MaxPopulation = authoring.maxPopulation,
                ReproductionRate = authoring.reproductionRate
            });

            AddComponent(entity, new VillagerPopulationData
            {
                TotalPopulation = 0,
                ActiveWorkers = 0,
                IdleVillagers = 0,
                CombatCapable = 0,
                AverageHealth = 0f,
                AverageHunger = 0f,
                AverageMorale = 0f,
                LastUpdateTick = 0
            });
        }
    }
}
