using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Aligns the Space4X project with the shared DOTS registries by mirroring colony and fleet data.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(RegistrySpatialSyncSystem))]
    public partial struct Space4XRegistryBridgeSystem : ISystem
    {
        private EntityQuery _colonyQuery;
        private EntityQuery _fleetQuery;

        private Entity _colonyRegistryEntity;
        private Entity _fleetRegistryEntity;
        private Entity _snapshotEntity;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RegistryDirectory>();
            state.RequireForUpdate<TimeState>();

            _colonyQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XColony, LocalTransform>()
                .Build();

            _fleetQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XFleet, LocalTransform>()
                .Build();

            var colonyLabel = new FixedString64Bytes("Space4X Colonies");
            var fleetLabel = new FixedString64Bytes("Space4X Fleets");

            _colonyRegistryEntity = EnsureRegistryEntity<Space4XColonyRegistry, Space4XColonyRegistryEntry>(ref state, colonyLabel, Space4XRegistryIds.ColonyArchetype);
            _fleetRegistryEntity = EnsureRegistryEntity<Space4XFleetRegistry, Space4XFleetRegistryEntry>(ref state, fleetLabel, Space4XRegistryIds.FleetArchetype);

            using var snapshotQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XRegistrySnapshot>());
            if (snapshotQuery.IsEmptyIgnoreFilter)
            {
                _snapshotEntity = state.EntityManager.CreateEntity(typeof(Space4XRegistrySnapshot));
            }
            else
            {
                _snapshotEntity = snapshotQuery.GetSingletonEntity();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;

            UpdateColonyRegistry(ref state, tick);
            UpdateFleetRegistry(ref state, tick);
        }

        private void UpdateColonyRegistry(ref SystemState state, uint tick)
        {
            var colonyCount = _colonyQuery.CalculateEntityCount();

            using var builder = new DeterministicRegistryBuilder<Space4XColonyRegistryEntry>(colonyCount, Allocator.Temp);

            float totalPopulation = 0f;
            float totalResources = 0f;

            foreach (var (colony, transform, entity) in SystemAPI.Query<RefRO<Space4XColony>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var colonyData = colony.ValueRO;
                var position = transform.ValueRO.Position;
                var flags = Space4XRegistryFlags.FromColonyStatus(colonyData.Status);

                builder.Add(new Space4XColonyRegistryEntry
                {
                    ColonyEntity = entity,
                    ColonyId = colonyData.ColonyId,
                    Population = colonyData.Population,
                    StoredResources = colonyData.StoredResources,
                    WorldPosition = position,
                    SectorId = colonyData.SectorId,
                    Status = colonyData.Status,
                    Flags = flags
                });

                totalPopulation += colonyData.Population;
                totalResources += colonyData.StoredResources;
            }

            var buffer = state.EntityManager.GetBuffer<Space4XColonyRegistryEntry>(_colonyRegistryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(_colonyRegistryEntity).ValueRW;
            builder.ApplyTo(ref buffer, ref metadata, tick, RegistryContinuitySnapshot.WithoutSpatialData());

            ref var summary = ref SystemAPI.GetComponentRW<Space4XColonyRegistry>(_colonyRegistryEntity).ValueRW;
            summary.ColonyCount = buffer.Length;
            summary.TotalPopulation = totalPopulation;
            summary.TotalStoredResources = totalResources;
            summary.LastUpdateTick = tick;

            ref var snapshot = ref SystemAPI.GetComponentRW<Space4XRegistrySnapshot>(_snapshotEntity).ValueRW;
            snapshot.ColonyCount = buffer.Length;
            snapshot.LastRegistryTick = math.max(snapshot.LastRegistryTick, tick);
        }

        private void UpdateFleetRegistry(ref SystemState state, uint tick)
        {
            var fleetCount = _fleetQuery.CalculateEntityCount();

            using var builder = new DeterministicRegistryBuilder<Space4XFleetRegistryEntry>(fleetCount, Allocator.Temp);

            int totalShips = 0;
            int engagementCount = 0;

            foreach (var (fleet, transform, entity) in SystemAPI.Query<RefRO<Space4XFleet>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var fleetData = fleet.ValueRO;
                var position = transform.ValueRO.Position;
                var flags = Space4XRegistryFlags.FromFleetPosture(fleetData.Posture);

                builder.Add(new Space4XFleetRegistryEntry
                {
                    FleetEntity = entity,
                    FleetId = fleetData.FleetId,
                    ShipCount = fleetData.ShipCount,
                    Posture = fleetData.Posture,
                    WorldPosition = position,
                    Flags = flags
                });

                totalShips += fleetData.ShipCount;
                if ((flags & Space4XRegistryFlags.FleetEngaging) != 0)
                {
                    engagementCount++;
                }
            }

            var buffer = state.EntityManager.GetBuffer<Space4XFleetRegistryEntry>(_fleetRegistryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(_fleetRegistryEntity).ValueRW;
            builder.ApplyTo(ref buffer, ref metadata, tick, RegistryContinuitySnapshot.WithoutSpatialData());

            ref var summary = ref SystemAPI.GetComponentRW<Space4XFleetRegistry>(_fleetRegistryEntity).ValueRW;
            summary.FleetCount = buffer.Length;
            summary.TotalShips = totalShips;
            summary.ActiveEngagementCount = engagementCount;
            summary.LastUpdateTick = tick;

            ref var snapshot = ref SystemAPI.GetComponentRW<Space4XRegistrySnapshot>(_snapshotEntity).ValueRW;
            snapshot.FleetCount = buffer.Length;
            snapshot.FleetEngagementCount = engagementCount;
            snapshot.LastRegistryTick = math.max(snapshot.LastRegistryTick, tick);
        }

        private static Entity EnsureRegistryEntity<TRegistry, TEntry>(ref SystemState state, FixedString64Bytes label, ushort archetypeId)
            where TRegistry : unmanaged, IComponentData
            where TEntry : unmanaged, IBufferElementData, IComparable<TEntry>
        {
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TRegistry>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            var entity = state.EntityManager.CreateEntity(typeof(TRegistry), typeof(RegistryMetadata));
            state.EntityManager.AddBuffer<TEntry>(entity);

            state.EntityManager.SetComponentData(entity, new TRegistry());

            var metadata = new RegistryMetadata();
            metadata.Initialise(RegistryKind.Custom, archetypeId, RegistryHandleFlags.None, label);
            state.EntityManager.SetComponentData(entity, metadata);

            return entity;
        }
    }

    /// <summary>
    /// Appends Space4X specific metrics to the shared telemetry buffer after the debug HUD snapshot is populated.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(DebugDisplaySystem))]
    public partial struct Space4XRegistryTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XRegistrySnapshot>();
            state.RequireForUpdate<TelemetryStream>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            var snapshot = SystemAPI.GetSingleton<Space4XRegistrySnapshot>();

            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            FixedString64Bytes key;

            key = "space4x.registry.colonies";
            buffer.Add(new TelemetryMetric { Key = key, Value = snapshot.ColonyCount, Unit = TelemetryMetricUnit.Count });

            key = "space4x.registry.fleets";
            buffer.Add(new TelemetryMetric { Key = key, Value = snapshot.FleetCount, Unit = TelemetryMetricUnit.Count });

            key = "space4x.registry.fleets.engaging";
            buffer.Add(new TelemetryMetric { Key = key, Value = snapshot.FleetEngagementCount, Unit = TelemetryMetricUnit.Count });
        }
    }
}
