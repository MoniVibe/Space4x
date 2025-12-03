#if SPACE4X_MIRACLES_WIP
// TODO: Update these tests to the new miracle API in PureDOTS.Runtime.Miracles and re-enable SPACE4X_MIRACLES_WIP.
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests.TestUtilities
{
    /// <summary>
    /// Utility class for setting up mock registries in tests.
    /// Provides helper methods to create registry entities and populate them with test data.
    /// </summary>
    public static class MockRegistryUtility
    {
        /// <summary>
        /// Ensures all required registry infrastructure exists for testing.
        /// </summary>
        public static void EnsureRegistryInfrastructure(EntityManager entityManager)
        {
            // Ensure RegistryDirectory
            using var directoryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryDirectory>());
            if (directoryQuery.IsEmptyIgnoreFilter)
            {
                var directoryEntity = entityManager.CreateEntity(typeof(RegistryDirectory), typeof(RegistryMetadata));
            }

            // Ensure TimeState
            CoreSingletonBootstrapSystem.EnsureSingletons(entityManager);

            // Ensure MiracleRegistry (required by bridge system)
            using var miracleQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PureDOTS.Runtime.Registry.MiracleRegistry>());
            if (miracleQuery.IsEmptyIgnoreFilter)
            {
                var miracleEntity = entityManager.CreateEntity(typeof(PureDOTS.Runtime.Registry.MiracleRegistry));
                entityManager.SetComponentData(miracleEntity, new PureDOTS.Runtime.Registry.MiracleRegistry
                {
                    TotalMiracles = 0,
                    ActiveMiracles = 0,
                    TotalEnergyCost = 0f,
                    TotalCooldownSeconds = 0f
                });
            }

            // Ensure SpatialGridConfig and SpatialGridState (optional but helpful)
            using var gridConfigQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpatialGridConfig>());
            if (gridConfigQuery.IsEmptyIgnoreFilter)
            {
                var gridEntity = entityManager.CreateEntity(typeof(SpatialGridConfig), typeof(SpatialGridState));
                entityManager.SetComponentData(gridEntity, default(SpatialGridConfig));
                entityManager.SetComponentData(gridEntity, default(SpatialGridState));
            }
        }

        /// <summary>
        /// Creates a test colony entity with all required components.
        /// </summary>
        public static Entity CreateTestColony(EntityManager entityManager, string colonyId, float population, float storedResources, Space4XColonyStatus status, float3 position)
        {
            var entity = entityManager.CreateEntity(typeof(Space4XColony), typeof(LocalTransform), typeof(SpatialIndexedTag));
            entityManager.SetComponentData(entity, new Space4XColony
            {
                ColonyId = new FixedString64Bytes(colonyId),
                Population = population,
                StoredResources = storedResources,
                Status = status,
                SectorId = 1
            });
            entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        /// <summary>
        /// Creates a test fleet entity with all required components.
        /// </summary>
        public static Entity CreateTestFleet(EntityManager entityManager, string fleetId, int shipCount, Space4XFleetPosture posture, float3 position)
        {
            var entity = entityManager.CreateEntity(typeof(Space4XFleet), typeof(LocalTransform), typeof(SpatialIndexedTag));
            entityManager.SetComponentData(entity, new Space4XFleet
            {
                FleetId = new FixedString64Bytes(fleetId),
                ShipCount = shipCount,
                Posture = posture,
                TaskForce = 0
            });
            entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        /// <summary>
        /// Creates a test logistics route entity with all required components.
        /// </summary>
        public static Entity CreateTestLogisticsRoute(EntityManager entityManager, string routeId, string originColonyId, string destinationColonyId, float dailyThroughput, float risk, Space4XLogisticsRouteStatus status, float3 position)
        {
            var entity = entityManager.CreateEntity(typeof(Space4XLogisticsRoute), typeof(LocalTransform), typeof(SpatialIndexedTag));
            entityManager.SetComponentData(entity, new Space4XLogisticsRoute
            {
                RouteId = new FixedString64Bytes(routeId),
                OriginColonyId = new FixedString64Bytes(originColonyId),
                DestinationColonyId = new FixedString64Bytes(destinationColonyId),
                DailyThroughput = dailyThroughput,
                Risk = risk,
                Priority = 1,
                Status = status
            });
            entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        /// <summary>
        /// Creates a test anomaly entity with all required components.
        /// </summary>
        public static Entity CreateTestAnomaly(EntityManager entityManager, string anomalyId, string classification, Space4XAnomalySeverity severity, Space4XAnomalyState state, float instability, int sectorId, float3 position)
        {
            var entity = entityManager.CreateEntity(typeof(Space4XAnomaly), typeof(LocalTransform), typeof(SpatialIndexedTag));
            entityManager.SetComponentData(entity, new Space4XAnomaly
            {
                AnomalyId = new FixedString64Bytes(anomalyId),
                Classification = new FixedString64Bytes(classification),
                Severity = severity,
                State = state,
                Instability = instability,
                SectorId = sectorId
            });
            entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        /// <summary>
        /// Finds the colony registry entity, creating it if it doesn't exist.
        /// </summary>
        public static Entity EnsureColonyRegistry(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XColonyRegistry>(),
                ComponentType.ReadOnly<Space4XColonyRegistryEntry>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            // Registry will be created by Space4XRegistryBridgeSystem on first update
            // For now, return Entity.Null
            return Entity.Null;
        }

        /// <summary>
        /// Finds the fleet registry entity, creating it if it doesn't exist.
        /// </summary>
        public static Entity EnsureFleetRegistry(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XFleetRegistry>(),
                ComponentType.ReadOnly<Space4XFleetRegistryEntry>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            return Entity.Null;
        }

        /// <summary>
        /// Finds the logistics registry entity, creating it if it doesn't exist.
        /// </summary>
        public static Entity EnsureLogisticsRegistry(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XLogisticsRegistry>(),
                ComponentType.ReadOnly<Space4XLogisticsRegistryEntry>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            return Entity.Null;
        }

        /// <summary>
        /// Finds the registry snapshot entity, creating it if it doesn't exist.
        /// </summary>
        public static Entity EnsureRegistrySnapshot(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XRegistrySnapshot>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            var entity = entityManager.CreateEntity(typeof(Space4XRegistrySnapshot));
            entityManager.SetComponentData(entity, new Space4XRegistrySnapshot
            {
                LastRegistryTick = 0
            });
            return entity;
        }

        /// <summary>
        /// Validates that a registry entry exists for the given entity.
        /// </summary>
        public static bool HasRegistryEntry<TEntry>(EntityManager entityManager, Entity registryEntity, Entity targetEntity)
            where TEntry : unmanaged, IBufferElementData, IRegistryEntry
        {
            if (registryEntity == Entity.Null || !entityManager.HasBuffer<TEntry>(registryEntity))
            {
                return false;
            }

            var buffer = entityManager.GetBuffer<TEntry>(registryEntity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].RegistryEntity == targetEntity)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif

