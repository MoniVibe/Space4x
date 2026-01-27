using PureDOTS.Runtime.Armies;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(RegistrySpatialSyncSystem))]
    public partial struct ArmyRegistrySystem : ISystem
    {
        private EntityQuery _armyQuery;
        private Entity _registryEntity;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;

        public void OnCreate(ref SystemState state)
        {
            _armyQuery = SystemAPI.QueryBuilder()
                .WithAll<ArmyId, LocalTransform>()
                .Build();
            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(true);

            _registryEntity = state.EntityManager.CreateEntity(
                typeof(ArmyRegistry),
                typeof(RegistryMetadata),
                typeof(ArmyRegistryEntry));

            state.RequireForUpdate(_armyQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);

            var expected = math.max(1, _armyQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<ArmyRegistryEntry>(expected, Allocator.Temp);

            var resolved = 0;
            var fallback = 0;
            var unmapped = 0;

            foreach (var (armyId, transform, entity) in SystemAPI.Query<RefRO<ArmyId>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var entry = new ArmyRegistryEntry
                {
                    ArmyEntity = entity,
                    Id = armyId.ValueRO,
                    Position = transform.ValueRO.Position,
                    Flags = 0,
                    CellId = -1,
                    SpatialVersion = 0
                };

                if (_residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    entry.CellId = residency.CellId;
                    entry.SpatialVersion = residency.Version;
                    resolved++;
                }
                else
                {
                    fallback++;
                }

                builder.Add(entry);
            }

            var metadata = state.EntityManager.GetComponentData<RegistryMetadata>(_registryEntity);
            var spatialVersion = metadata.SupportsSpatialQueries ? (uint)1 : (uint)0; // Convert bool to uint for spatial version
            var continuity = RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolved, fallback, unmapped, false);
            var buffer = state.EntityManager.GetBuffer<ArmyRegistryEntry>(_registryEntity);
            builder.ApplyTo(ref buffer, ref metadata, tick, continuity);

            state.EntityManager.SetComponentData(_registryEntity, new ArmyRegistry
            {
                ArmyCount = expected,
                LastUpdateTick = tick
            });
            state.EntityManager.SetComponentData(_registryEntity, metadata);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
