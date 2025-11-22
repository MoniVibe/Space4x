using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Entities;

namespace Space4X.Tests
{
    /// <summary>
    /// Minimal bootstrap utility to ensure required singletons exist for system tests.
    /// </summary>
    public static class CoreSingletonBootstrapSystem
    {
        public static void EnsureSingletons(EntityManager entityManager)
        {
            EnsureSingleton(entityManager, new TimeState
            {
                Tick = 0,
                FixedDeltaTime = 0.1f,
                IsPaused = false
            });

            EnsureSingleton(entityManager, new RewindState
            {
                Mode = RewindMode.Record
            });

            EnsureSingleton(entityManager, new GameplayFixedStep
            {
                FixedDeltaTime = 0.1f
            });
        }

        public static void EnsureMiningSpine(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XMiningTimeSpine>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = entityManager.CreateEntity(
                typeof(Space4XMiningTimeSpine),
                typeof(MiningSnapshot),
                typeof(MiningTelemetrySnapshot),
                typeof(MiningCommandLogEntry),
                typeof(SkillChangeLogEntry),
                typeof(SkillSnapshot));

            entityManager.GetBuffer<MiningSnapshot>(entity);
            entityManager.GetBuffer<MiningTelemetrySnapshot>(entity);
            entityManager.GetBuffer<MiningCommandLogEntry>(entity);
            entityManager.GetBuffer<SkillChangeLogEntry>(entity);
            entityManager.GetBuffer<SkillSnapshot>(entity);

            entityManager.SetComponentData(entity, new Space4XMiningTimeSpine
            {
                LastSnapshotTick = 0,
                LastPlaybackTick = 0,
                SnapshotHorizon = Space4XMiningTimeSpine.DefaultSnapshotHorizon
            });
        }

        public static void EnsureCrewGrowthTelemetry(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CrewGrowthTelemetry>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = entityManager.CreateEntity(
                typeof(CrewGrowthTelemetry),
                typeof(CrewGrowthCommandLogEntry));

            entityManager.AddBuffer<CrewGrowthCommandLogEntry>(entity);
            entityManager.SetComponentData(entity, new CrewGrowthTelemetry
            {
                LastUpdateTick = 0,
                BreedingAttempts = 0,
                CloningAttempts = 0,
                GrowthSkipped = 0
            });
        }

        public static void EnsureFleetInterceptQueue(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XFleetInterceptQueue>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = entityManager.CreateEntity(
                typeof(Space4XFleetInterceptQueue),
                typeof(Space4XFleetInterceptTelemetry));

            entityManager.AddBuffer<InterceptRequest>(entity);
            entityManager.AddBuffer<FleetInterceptCommandLogEntry>(entity);
            entityManager.SetComponentData(entity, new Space4XFleetInterceptTelemetry
            {
                LastAttemptTick = 0,
                InterceptAttempts = 0,
                RendezvousAttempts = 0
            });
        }

        public static void EnsureTechDiffusionTelemetry(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TechDiffusionTelemetry>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = entityManager.CreateEntity(
                typeof(TechDiffusionTelemetry));
            entityManager.AddBuffer<TechDiffusionCommandLogEntry>(entity);
            entityManager.SetComponentData(entity, new TechDiffusionTelemetry
            {
                LastUpdateTick = 0,
                LastUpgradeTick = 0,
                ActiveDiffusions = 0,
                CompletedUpgrades = 0
            });
        }

        public static void EnsureModuleMaintenanceTelemetry(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ModuleMaintenanceLog>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = entityManager.CreateEntity(
                typeof(ModuleMaintenanceLog),
                typeof(ModuleMaintenanceTelemetry));

            entityManager.AddBuffer<ModuleMaintenanceCommandLogEntry>(entity);
            entityManager.SetComponentData(entity, new ModuleMaintenanceTelemetry
            {
                LastUpdateTick = 0,
                RefitStarted = 0,
                RefitCompleted = 0,
                Failures = 0,
                RepairApplied = 0f,
                RefitWorkApplied = 0f
            });
            entityManager.SetComponentData(entity, new ModuleMaintenanceLog
            {
                SnapshotHorizon = 512,
                LastPlaybackTick = 0
            });
        }

        private static void EnsureSingleton<T>(EntityManager entityManager, T data)
            where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(T));
                entityManager.SetComponentData(entity, data);
            }
        }
    }
}
