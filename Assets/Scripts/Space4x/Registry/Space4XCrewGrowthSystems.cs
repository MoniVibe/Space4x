using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures telemetry/log buffers exist for crew growth decisions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XCrewGrowthBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<CrewGrowthTelemetry>(out _))
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity(
                typeof(CrewGrowthTelemetry),
                typeof(CrewGrowthCommandLogEntry));

            state.EntityManager.AddBuffer<CrewGrowthCommandLogEntry>(entity);
            state.EntityManager.SetComponentData(entity, new CrewGrowthTelemetry
            {
                LastUpdateTick = 0,
                BreedingAttempts = 0,
                CloningAttempts = 0,
                GrowthSkipped = 0
            });

            state.Enabled = false;
        }
    }

    /// <summary>
    /// Stubbed growth system gated by policy/tech toggles. Defaults to no-ops when disabled.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    // Removed invalid UpdateAfter: GameplayFixedStepSyncSystem runs in TimeSystemGroup.
    public partial struct Space4XCrewGrowthSystem : ISystem
    {
        private ComponentLookup<CrewGrowthState> _stateLookup;

        public void OnCreate(ref SystemState state)
        {
            _stateLookup = state.GetComponentLookup<CrewGrowthState>(false);
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();

            if (time.IsPaused || rewind.Mode == RewindMode.Playback)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<CrewGrowthTelemetry>(out var telemetryEntity))
            {
                return;
            }

            var commandLog = state.EntityManager.GetBuffer<CrewGrowthCommandLogEntry>(telemetryEntity);
            var telemetry = state.EntityManager.GetComponentData<CrewGrowthTelemetry>(telemetryEntity);

            _stateLookup.Update(ref state);
            telemetry.LastUpdateTick = time.Tick;

            foreach (var (settings, entity) in SystemAPI.Query<RefRO<CrewGrowthSettings>>().WithEntityAccess())
            {
                var breedingActive = settings.ValueRO.BreedingEnabled != 0 &&
                                     settings.ValueRO.DoctrineAllowsBreeding != 0 &&
                                     settings.ValueRO.BreedingRatePerTick > 0f;

                var cloningActive = settings.ValueRO.CloningEnabled != 0 &&
                                    settings.ValueRO.DoctrineAllowsCloning != 0 &&
                                    settings.ValueRO.CloningRatePerTick > 0f;

                if (!breedingActive && !cloningActive)
                {
                    telemetry.GrowthSkipped += 1;
                    continue;
                }

                if (breedingActive)
                {
                    telemetry.BreedingAttempts += 1;
                }

                if (cloningActive)
                {
                    telemetry.CloningAttempts += 1;
                }

                // Stub: record intent but do not mutate crew counts until policy/tech integration completes.
                if (commandLog.IsCreated)
                {
                    commandLog.Add(new CrewGrowthCommandLogEntry
                    {
                        Tick = time.Tick,
                        TargetEntity = entity,
                        DeltaCrew = 0f,
                        WasBreeding = (byte)(breedingActive ? 1 : 0),
                        WasCloning = (byte)(cloningActive ? 1 : 0),
                        SkippedByPolicy = 1
                    });
                }
            }

            state.EntityManager.SetComponentData(telemetryEntity, telemetry);
        }
    }

    /// <summary>
    /// Publishes crew growth counters for HUD/debug bindings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XCrewSkillTelemetrySystem))]
    public partial struct Space4XCrewGrowthTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryStreamQuery;
        private EntityQuery _growthTelemetryQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<CrewGrowthTelemetry>();

            _telemetryStreamQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _growthTelemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<CrewGrowthTelemetry>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = _growthTelemetryQuery.GetSingletonEntity();
            var metrics = state.EntityManager.GetComponentData<CrewGrowthTelemetry>(telemetryEntity);

            var streamEntity = _telemetryStreamQuery.GetSingletonEntity();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(streamEntity);

            buffer.AddMetric("space4x.crew.growth.breedingAttempts", metrics.BreedingAttempts);
            buffer.AddMetric("space4x.crew.growth.cloningAttempts", metrics.CloningAttempts);
            buffer.AddMetric("space4x.crew.growth.skipped", metrics.GrowthSkipped);
            buffer.AddMetric("space4x.crew.growth.lastTick", metrics.LastUpdateTick);
        }
    }
}
