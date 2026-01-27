using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.TestUtilities
{
    /// <summary>
    /// Test world wrapper for registry testing with proper setup and disposal.
    /// </summary>
    public class RegistryTestWorld : IDisposable
    {
        /// <summary>
        /// The underlying ECS world.
        /// </summary>
        public World World { get; private set; }

        /// <summary>
        /// Entity manager shortcut.
        /// </summary>
        public EntityManager EntityManager => World.EntityManager;

        /// <summary>
        /// Configuration used to create this test world.
        /// </summary>
        public RegistryTestConfig Config { get; private set; }

        /// <summary>
        /// Entity holding the TimeState singleton (if created).
        /// </summary>
        public Entity TimeStateEntity { get; private set; }

        /// <summary>
        /// Entity holding the RewindState singleton (if created).
        /// </summary>
        public Entity RewindStateEntity { get; private set; }

        /// <summary>
        /// Entity holding the TelemetryStream singleton (if created).
        /// </summary>
        public Entity TelemetryStreamEntity { get; private set; }

        /// <summary>
        /// Entity holding the RegistryDirectory singleton (if created).
        /// </summary>
        public Entity RegistryDirectoryEntity { get; private set; }

        /// <summary>
        /// Current tick value (for manual simulation).
        /// </summary>
        public uint CurrentTick { get; private set; }

        private bool _disposed;

        private RegistryTestWorld()
        {
        }

        /// <summary>
        /// Creates a registry test world with the specified configuration.
        /// </summary>
        public static RegistryTestWorld Create(RegistryTestConfig config)
        {
            var testWorld = new RegistryTestWorld
            {
                Config = config,
                CurrentTick = config.InitialTick
            };

            testWorld.World = new World(config.WorldName);

            if (config.BootCoreSingletons)
            {
                testWorld.BootCoreSingletons();
            }

            if (config.CreateTelemetryStream)
            {
                testWorld.CreateTelemetryStream();
            }

            if (config.CreateRegistryDirectory)
            {
                testWorld.CreateRegistryDirectory();
            }

            return testWorld;
        }

        /// <summary>
        /// Creates a registry test world with default configuration.
        /// </summary>
        public static RegistryTestWorld CreateDefault(string worldName = "TestWorld")
        {
            return Create(RegistryTestConfig.CreateDefault(worldName));
        }

        /// <summary>
        /// Advances the simulation by one tick.
        /// </summary>
        public void Tick()
        {
            CurrentTick++;

            if (TimeStateEntity != Entity.Null && EntityManager.Exists(TimeStateEntity))
            {
                var timeState = EntityManager.GetComponentData<TimeState>(TimeStateEntity);
                timeState.Tick = CurrentTick;
                timeState.FixedDeltaTime = Config.FixedDeltaTime;
                EntityManager.SetComponentData(TimeStateEntity, timeState);
            }

            World.Update();
        }

        /// <summary>
        /// Advances the simulation by multiple ticks.
        /// </summary>
        public void Tick(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Tick();
            }
        }

        /// <summary>
        /// Updates only systems in the specified group.
        /// </summary>
        public void StepSystems<TGroup>() where TGroup : ComponentSystemGroup
        {
            var group = World.GetExistingSystemManaged<TGroup>();
            group?.Update();
        }

        /// <summary>
        /// Gets or creates a singleton component.
        /// </summary>
        public T GetOrCreateSingleton<T>() where T : unmanaged, IComponentData
        {
            var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingleton<T>();
            }

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, default(T));
            return default;
        }

        /// <summary>
        /// Gets or creates a singleton entity with the specified component.
        /// </summary>
        public Entity GetOrCreateSingletonEntity<T>() where T : unmanaged, IComponentData
        {
            var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, default(T));
            return entity;
        }

        /// <summary>
        /// Sets the TimeState singleton values.
        /// </summary>
        public void SetTimeState(uint tick, float deltaTime = 1f / 60f, bool isPaused = false)
        {
            if (TimeStateEntity == Entity.Null)
            {
                return;
            }

            CurrentTick = tick;
            EntityManager.SetComponentData(TimeStateEntity, new TimeState
            {
                Tick = tick,
                FixedDeltaTime = deltaTime,
                IsPaused = isPaused
            });
        }

        /// <summary>
        /// Sets the RewindState singleton values.
        /// </summary>
        public void SetRewindState(RewindMode mode = RewindMode.Record)
        {
            if (RewindStateEntity == Entity.Null)
            {
                return;
            }

            EntityManager.SetComponentData(RewindStateEntity, new RewindState
            {
                Mode = mode
            });
            if (EntityManager.HasComponent<RewindLegacyState>(RewindStateEntity))
            {
                var legacy = EntityManager.GetComponentData<RewindLegacyState>(RewindStateEntity);
                legacy.PlaybackTick = CurrentTick;
                EntityManager.SetComponentData(RewindStateEntity, legacy);
            }
        }

        private void BootCoreSingletons()
        {
            // TimeState
            TimeStateEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(TimeStateEntity, new TimeState
            {
                Tick = Config.InitialTick,
                FixedDeltaTime = Config.FixedDeltaTime,
                IsPaused = false
            });

            // RewindState
            RewindStateEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(RewindStateEntity, new RewindState
            {
                Mode = RewindMode.Record,
                TargetTick = (int)Config.InitialTick,
                TickDuration = Config.FixedDeltaTime,
                MaxHistoryTicks = 600,
                PendingStepTicks = 0
            });
            EntityManager.AddComponentData(RewindStateEntity, new RewindLegacyState
            {
                PlaybackSpeed = 1f,
                CurrentTick = (int)Config.InitialTick,
                StartTick = 0,
                PlaybackTick = Config.InitialTick,
                PlaybackTicksPerSecond = Config.FixedDeltaTime > 0f ? 1f / Config.FixedDeltaTime : 60f,
                ScrubDirection = 0,
                ScrubSpeedMultiplier = 1f,
                RewindWindowTicks = 0,
                ActiveTrack = default
            });

            // RegistrySpatialSyncState
            var spatialSyncEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(spatialSyncEntity, new RegistrySpatialSyncState());
            EntityManager.AddBuffer<RegistryContinuityAlert>(spatialSyncEntity);
            EntityManager.AddComponentData(spatialSyncEntity, new RegistryContinuityState());
        }

        private void CreateTelemetryStream()
        {
            TelemetryStreamEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(TelemetryStreamEntity, new TelemetryStream
            {
                Version = 0,
                LastTick = 0
            });
            EntityManager.AddBuffer<TelemetryMetric>(TelemetryStreamEntity);
            TelemetryStreamUtility.EnsureEventStream(EntityManager);
        }

        private void CreateRegistryDirectory()
        {
            RegistryDirectoryEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(RegistryDirectoryEntity, new RegistryDirectory
            {
                Version = 0,
                LastUpdateTick = 0,
                AggregateHash = 0
            });
            EntityManager.AddBuffer<RegistryDirectoryEntry>(RegistryDirectoryEntity);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (World != null && World.IsCreated)
            {
                World.Dispose();
            }

            World = null;
        }
    }
}
