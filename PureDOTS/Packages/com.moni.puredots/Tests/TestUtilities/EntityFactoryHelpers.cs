using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.TestUtilities
{
    /// <summary>
    /// Factory helpers for creating test entities with common component configurations.
    /// </summary>
    public static class EntityFactoryHelpers
    {
        /// <summary>
        /// Creates a miracle entity for testing.
        /// </summary>
        public static Entity CreateMiracle(World world, MiracleTestParams parameters)
        {
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            entityManager.AddComponentData(entity, new MiracleDefinition
            {
                Type = parameters.Type,
                CastingMode = parameters.CastingMode,
                BaseRadius = 5f,
                BaseIntensity = 1f,
                BaseCost = 10f,
                SustainedCostPerSecond = 1f
            });

            entityManager.AddComponentData(entity, new MiracleRuntimeState
            {
                Lifecycle = MiracleLifecycleState.Charging,
                ChargePercent = 0f,
                CurrentRadius = 0f,
                CurrentIntensity = 0f,
                CooldownSecondsRemaining = 0f,
                LastCastTick = 0u,
                AlignmentDelta = 0
            });

            if (parameters.CasterEntity != Entity.Null)
            {
                entityManager.AddComponentData(entity, new MiracleCaster
                {
                    CasterEntity = parameters.CasterEntity,
                    HandEntity = Entity.Null
                });
            }

            entityManager.AddComponentData(entity, LocalTransform.FromPosition(parameters.TargetPosition));

            return entity;
        }

        /// <summary>
        /// Creates a villager entity for testing.
        /// </summary>
        public static Entity CreateVillager(World world, int villagerId = 1, int factionId = 1, float3 position = default)
        {
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            entityManager.AddComponentData(entity, new VillagerId
            {
                Value = villagerId,
                FactionId = factionId
            });

            entityManager.AddComponentData(entity, new VillagerNeeds
            {
                Food = 100,
                Rest = 100,
                Sleep = 100,
                GeneralHealth = 100,
                Health = 100f,
                MaxHealth = 100f,
                Energy = 100f,
                Hunger = 100f,
                Morale = 75f,
                Temperature = 20f
            });

            entityManager.AddComponentData(entity, new VillagerMood
            {
                Mood = 75f,
                TargetMood = 75f,
                MoodChangeRate = 1f,
                Wellbeing = 75f
            });

            entityManager.AddComponentData(entity, new VillagerCombatStats
            {
                AttackDamage = 10f,
                AttackSpeed = 1f,
                CurrentTarget = Entity.Null
            });

            entityManager.AddComponentData(entity, new VillagerAIState
            {
                CurrentState = VillagerAIState.State.Idle,
                CurrentGoal = VillagerAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0u
            });

            entityManager.AddComponentData(entity, new VillagerJob
            {
                Type = VillagerJob.JobType.None,
                Phase = VillagerJob.JobPhase.Idle,
                ActiveTicketId = 0u,
                Productivity = 1f,
                LastStateChangeTick = 0u
            });

            entityManager.AddComponentData(entity, new VillagerAvailability
            {
                IsAvailable = 1,
                IsReserved = 0,
                LastChangeTick = 0u,
                BusyTime = 0f
            });

            entityManager.AddComponentData(entity, new VillagerFlags());
            entityManager.AddComponentData(entity, LocalTransform.FromPosition(position));

            return entity;
        }

        /// <summary>
        /// Creates a resource source entity for testing.
        /// </summary>
        public static Entity CreateResourceSource(
            World world,
            ResourceSourceType sourceType = ResourceSourceType.Default,
            float currentAmount = 100f,
            float maxAmount = 100f,
            float3 position = default)
        {
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            var units = math.min(currentAmount, maxAmount);
            entityManager.AddComponentData(entity, new ResourceSourceState
            {
                SourceType = sourceType,
                UnitsRemaining = units,
                QualityTier = ResourceQualityTier.Unknown,
                BaseQuality = 0,
                QualityVariance = 0
            });

            entityManager.AddComponentData(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = 1f,
                MaxSimultaneousWorkers = 4,
                RespawnSeconds = 60f,
                Flags = 0,
                LessonId = default
            });

            entityManager.AddComponentData(entity, LocalTransform.FromPosition(position));

            return entity;
        }

        /// <summary>
        /// Creates a storehouse entity for testing.
        /// </summary>
        public static Entity CreateStorehouse(
            World world,
            int storehouseId = 1,
            float capacity = 1000f,
            float3 position = default)
        {
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            entityManager.AddComponentData(entity, new StorehouseConfig
            {
                ShredRate = 0f,
                MaxShredQueueSize = 0,
                InputRate = 0f,
                OutputRate = 0f,
                Label = default
            });

            entityManager.AddComponentData(entity, new StorehouseInventory
            {
                TotalCapacity = capacity,
                TotalStored = 0f,
                ItemTypeCount = 0,
                IsShredding = 0,
                LastUpdateTick = 0
            });

            entityManager.AddBuffer<StorehouseInventoryItem>(entity);
            entityManager.AddBuffer<StorehouseCapacityElement>(entity);
            entityManager.AddComponentData(entity, LocalTransform.FromPosition(position));

            return entity;
        }

        /// <summary>
        /// Creates a basic registry singleton entity.
        /// </summary>
        /// <typeparam name="TRegistry">The registry component type.</typeparam>
        /// <typeparam name="TEntry">The registry entry buffer element type.</typeparam>
        public static Entity CreateRegistry<TRegistry, TEntry>(
            World world,
            RegistryKind kind,
            string label = "TestRegistry")
            where TRegistry : unmanaged, IComponentData
            where TEntry : unmanaged, IBufferElementData
        {
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            entityManager.AddComponentData(entity, default(TRegistry));
            entityManager.AddBuffer<TEntry>(entity);

            var metadata = new RegistryMetadata();
            metadata.Initialise(
                kind,
                0,
                RegistryHandleFlags.None,
                new FixedString64Bytes(label));
            entityManager.AddComponentData(entity, metadata);

            return entity;
        }
    }
}
