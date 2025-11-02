using PureDOTS.Presentation.Runtime;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;
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

        [Header("Movement")]
        [Range(0.1f, 10f)] public float baseSpeed = 3f;
        [Range(1f, 50f)] public float visionRange = 20f;
        [Range(1f, 30f)] public float hearingRange = 15f;

        [Header("Job")]
        public VillagerJob.JobType initialJob = VillagerJob.JobType.None;
        public GameObject initialWorksite;

        [Header("Discipline & Mood")]
        public VillagerDisciplineType initialDiscipline = VillagerDisciplineType.Unassigned;
        [Range(0, 10)] public byte initialDisciplineLevel;
        [Range(0f, 100f)] public float initialMood = 50f;
        [Range(0.1f, 5f)] public float moodChangeRate = 1f;
        public bool startAvailableForJobs = true;

        [Header("Combat (Optional)")]
        public bool isCombatCapable;
        [Range(0f, 100f)] public float attackDamage = 5f;
        [Range(0.1f, 5f)] public float attackSpeed = 1f;
        [Range(0f, 100f)] public float defenseRating = 10f;
        [Range(0f, 10f)] public float attackRange = 2f;
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

            AddComponent(entity, new VillagerNeeds
            {
                Health = authoring.initialHealth,
                MaxHealth = authoring.maxHealth,
                Hunger = authoring.initialHunger,
                Energy = authoring.initialEnergy,
                Morale = authoring.initialMorale,
                Temperature = 20f
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

            Entity worksiteEntity = authoring.initialWorksite != null
                ? GetEntity(authoring.initialWorksite, TransformUsageFlags.Dynamic)
                : Entity.Null;

            AddComponent(entity, new VillagerJob
            {
                Type = authoring.initialJob,
                WorksiteEntity = worksiteEntity,
                WorkProgress = 0f,
                Productivity = 1f
            });

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
                BaseSpeed = authoring.baseSpeed,
                CurrentSpeed = authoring.baseSpeed,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                IsStuck = 0,
                LastMoveTick = 0
            });

            AddComponent(entity, new VillagerSensors
            {
                VisionRange = authoring.visionRange,
                HearingRange = authoring.hearingRange,
                NearestThreat = Entity.Null,
                NearestFood = Entity.Null,
                NearestShelter = Entity.Null,
                LastKnownThreatPosition = float3.zero,
                LastSensorUpdateTick = 0
            });

            if (authoring.isCombatCapable)
            {
                AddComponent(entity, new VillagerCombatStats
                {
                    AttackDamage = authoring.attackDamage,
                    AttackSpeed = authoring.attackSpeed,
                    DefenseRating = authoring.defenseRating,
                    AttackRange = authoring.attackRange,
                    CurrentTarget = Entity.Null,
                    LastAttackTime = 0f
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
            AddBuffer<VillagerRelationship>(entity);
            AddBuffer<VillagerPathWaypoint>(entity);
            AddBuffer<VillagerMemoryEvent>(entity);
            AddBuffer<VillagerHistorySample>(entity);

            AddComponent<RewindableTag>(entity);
            AddComponent(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.Critical,
                OverrideStrideSeconds = 0f
            });

            AddComponent(entity, PresentationRequest.Create(PresentationPrototype.Villager));
            AddComponent(entity, new HandInteractable
            {
                Type = HandInteractableType.Villager,
                Radius = math.max(1.5f, authoring.visionRange * 0.1f)
            });
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
