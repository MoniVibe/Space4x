using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Orders;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Deterministic spawner lifecycle telemetry. Uses registry entries plus registry definitions to surface readiness and attempts.
    /// </summary>
    [UpdateInGroup(typeof(EnvironmentSystemGroup), OrderLast = true)]
    public partial struct SpawnerLifecycleSystem : ISystem
    {
        private ComponentLookup<SpawnerId> _spawnerIdLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnerRegistry>();
            state.RequireForUpdate<SpawnerTelemetry>();
            _spawnerIdLookup = state.GetComponentLookup<SpawnerId>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                return;
            }

            if (timeState.IsPaused || (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record))
            {
                return;
            }

            var telemetryEntity = SystemAPI.GetSingletonEntity<SpawnerTelemetry>();
            var telemetry = SystemAPI.GetComponentRW<SpawnerTelemetry>(telemetryEntity);
            var registryEntity = SystemAPI.GetSingletonEntity<SpawnerRegistry>();
            var entries = state.EntityManager.GetBuffer<SpawnerRegistryEntry>(registryEntity);

            var hasOrderStream = SystemAPI.TryGetSingletonEntity<OrderEventStream>(out var orderStreamEntity);
            var hasSignalBus = SystemAPI.TryGetSingletonEntity<SignalBus>(out var signalBusEntity);
            var orderEvents = hasOrderStream && state.EntityManager.HasBuffer<OrderEvent>(orderStreamEntity)
                ? state.EntityManager.GetBuffer<OrderEvent>(orderStreamEntity)
                : default;
            var signalEvents = hasSignalBus && state.EntityManager.HasBuffer<SignalEvent>(signalBusEntity)
                ? state.EntityManager.GetBuffer<SignalEvent>(signalBusEntity)
                : default;

            var hasCatalog = SystemAPI.TryGetSingleton(out RegistryDefinitionCatalog catalog) &&
                             catalog.IsCreated &&
                             catalog.Catalog.Value.Definitions.Length > 0;
            var catalogVersion = hasCatalog ? (uint)catalog.Catalog.Value.Definitions.Length : 0u;

            _spawnerIdLookup.Update(ref state);

            int total = 0;
            int ready = 0;
            int cooling = 0;
            int disabled = 0;
            int attempts = 0;
            int spawned = 0;
            int failures = 0;

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                total++;

                if ((entry.Flags & SpawnerStatusFlags.Disabled) != 0)
                {
                    disabled++;
                    continue;
                }

                var capacityAvailable = entry.Capacity <= 0 || entry.ActiveSpawnCount < entry.Capacity;
                var readyToSpawn = capacityAvailable && entry.RemainingCooldown <= 0f;
                if (!readyToSpawn)
                {
                    if (entry.RemainingCooldown > 0f)
                    {
                        cooling++;
                    }
                    continue;
                }

                ready++;

                var spawnerId = _spawnerIdLookup.HasComponent(entry.SpawnerEntity)
                    ? _spawnerIdLookup[entry.SpawnerEntity].Value
                    : entry.SpawnerEntity.Index;

                uint seed = math.hash(new uint3((uint)spawnerId, timeState.Tick + 1u, (uint)entry.SpawnerEntity.Version));
                seed = seed == 0 ? 1u : seed;
                var random = Unity.Mathematics.Random.CreateFromIndex(seed);
                var roll = random.NextFloat();

                attempts++;
                var succeeded = roll > 0.25f;
                if (succeeded)
                {
                    spawned++;
                }
                else
                {
                    failures++;
                }

                if (orderEvents.IsCreated)
                {
                    var payload = new FixedString128Bytes();
                    payload.Append("roll=");
                    payload.Append(roll);
                    orderEvents.Add(new OrderEvent
                    {
                        OrderEntity = entry.SpawnerEntity,
                        EventType = succeeded ? OrderEventType.Started : OrderEventType.Failed,
                        OrderType = entry.SpawnerTypeId,
                        Payload = payload,
                        Value = roll,
                        Tick = timeState.Tick
                    });
                }

                if (signalEvents.IsCreated)
                {
                    signalEvents.Add(new SignalEvent
                    {
                        Channel = succeeded ? new FixedString64Bytes("spawner.ready") : new FixedString64Bytes("spawner.fail"),
                        Payload = entry.SpawnerTypeId,
                        Position = entry.Position,
                        Source = entry.SpawnerEntity,
                        Severity = succeeded ? (byte)0 : (byte)1,
                        Tick = timeState.Tick
                    });
                }
            }

            telemetry.ValueRW = new SpawnerTelemetry
            {
                TotalSpawners = total,
                ReadySpawners = ready,
                CoolingSpawners = cooling,
                DisabledSpawners = disabled,
                SpawnAttempts = attempts,
                Spawned = spawned,
                SpawnFailures = failures,
                LastUpdateTick = timeState.Tick,
                CatalogVersion = catalogVersion
            };
        }
    }
}
