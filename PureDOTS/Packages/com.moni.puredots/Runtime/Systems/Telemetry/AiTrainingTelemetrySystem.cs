using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Structures;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Villager;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Telemetry
{
    /// <summary>
    /// Minimal, headless-friendly telemetry for AI training baselines.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct AiTrainingTelemetrySystem : ISystem
    {
        private EntityQuery _villagerQuery;
        private EntityQuery _shipQuery;
        private EntityQuery _structureQuery;

        private FixedString64Bytes _metricEntitiesTotal;
        private FixedString64Bytes _metricUnitsMobile;
        private FixedString64Bytes _metricBuildingsTotal;
        private FixedString64Bytes _metricTick;
        private FixedString64Bytes _metricWorldSeconds;
        private FixedString64Bytes _metricHeartbeat;
        private FixedString64Bytes _eventType;
        private FixedString64Bytes _eventSource;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TimeState>();

            _villagerQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<VillagerId>(),
                ComponentType.ReadOnly<VillagerFlags>());
            _shipQuery = state.GetEntityQuery(ComponentType.ReadOnly<ShipAggregate>());
            _structureQuery = state.GetEntityQuery(ComponentType.ReadOnly<StructureDurability>());

            _metricEntitiesTotal = "entities.total";
            _metricUnitsMobile = "units.mobile";
            _metricBuildingsTotal = "buildings.total";
            _metricTick = "time.tick";
            _metricWorldSeconds = "time.worldSeconds";
            _metricHeartbeat = "telemetry.heartbeat";
            _eventType = "ai_action";
            _eventSource = "ai";
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var exportConfig) || exportConfig.Enabled == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint effectiveTick = timeState.Tick;
            float effectiveWorldSeconds = timeState.WorldSeconds;
            if (SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioTick) && scenarioTick.Tick > 0)
            {
                effectiveTick = scenarioTick.Tick;
                effectiveWorldSeconds = scenarioTick.WorldSeconds;
            }
            else
            {
                var elapsed = (float)SystemAPI.Time.ElapsedTime;
                var delta = (float)SystemAPI.Time.DeltaTime;
                effectiveTick = GetEffectiveTick(timeState.Tick, elapsed, delta);
                effectiveWorldSeconds = GetEffectiveWorldSeconds(timeState.WorldSeconds, elapsed);
            }

            var cadence = exportConfig.CadenceTicks > 0 ? exportConfig.CadenceTicks : 30u;
            if (effectiveTick % cadence != 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity))
            {
                return;
            }

            if (!state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return;
            }

            if ((exportConfig.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) != 0)
            {
                var metrics = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
                var totalEntities = state.EntityManager.UniversalQuery.CalculateEntityCount();
                var mobileUnits = _villagerQuery.CalculateEntityCount() + _shipQuery.CalculateEntityCount();
                var totalBuildings = _structureQuery.CalculateEntityCount();

                metrics.AddMetric(_metricEntitiesTotal, totalEntities, TelemetryMetricUnit.Count);
                metrics.AddMetric(_metricUnitsMobile, mobileUnits, TelemetryMetricUnit.Count);
                metrics.AddMetric(_metricBuildingsTotal, totalBuildings, TelemetryMetricUnit.Count);
                metrics.AddMetric(_metricTick, effectiveTick, TelemetryMetricUnit.Count);
                metrics.AddMetric(_metricWorldSeconds, effectiveWorldSeconds, TelemetryMetricUnit.None);
                metrics.AddMetric(_metricHeartbeat, 1f, TelemetryMetricUnit.Count);
            }

            if ((exportConfig.Flags & TelemetryExportFlags.IncludeTelemetryEvents) != 0)
            {
                var maxEvents = exportConfig.MaxEventsPerTick > 0 ? exportConfig.MaxEventsPerTick : (ushort)64;
                EmitAiActionEvents(ref state, maxEvents);
            }
        }

        private void EmitAiActionEvents(ref SystemState state, ushort maxEvents)
        {
            if (!SystemAPI.TryGetSingletonBuffer<BehaviorTelemetryRecord>(out var behaviorBuffer))
            {
                return;
            }

            if (behaviorBuffer.Length == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var streamRef))
            {
                return;
            }

            if (streamRef.Stream == Entity.Null || !state.EntityManager.HasBuffer<TelemetryEvent>(streamRef.Stream))
            {
                return;
            }

            var eventBuffer = state.EntityManager.GetBuffer<TelemetryEvent>(streamRef.Stream);
            var count = behaviorBuffer.Length > maxEvents ? maxEvents : behaviorBuffer.Length;

            for (int i = 0; i < count; i++)
            {
                var record = behaviorBuffer[i];
                var payload = BuildPayload(record);
                eventBuffer.AddEvent(_eventType, record.Tick, _eventSource, payload);
            }
        }

        private static FixedString128Bytes BuildPayload(in BehaviorTelemetryRecord record)
        {
            var payload = new FixedString128Bytes();
            payload.Append('{');
            payload.Append("\"b\":");
            payload.Append((int)record.Behavior);
            payload.Append(",\"k\":");
            payload.Append((int)record.MetricOrInvariantId);
            payload.Append(",\"v\":");
            var value = record.Kind == BehaviorTelemetryRecordKind.Metric ? (int)record.ValueA : record.Passed;
            payload.Append(value);
            payload.Append(",\"t\":");
            payload.Append((byte)record.Kind);
            payload.Append('}');
            return payload;
        }

        private static uint GetEffectiveTick(uint tick, float elapsed, float delta)
        {
            if (tick == 0 && UnityEngine.Application.isBatchMode)
            {
                if (delta > 0f && elapsed > 0f)
                {
                    var elapsedTick = (uint)(elapsed / delta);
                    if (elapsedTick > tick)
                    {
                        tick = elapsedTick;
                    }
                }
            }

            return tick;
        }

        private static float GetEffectiveWorldSeconds(float worldSeconds, float elapsed)
        {
            var seconds = worldSeconds;
            if (seconds <= 0f && UnityEngine.Application.isBatchMode)
            {
                if (elapsed > seconds)
                {
                    seconds = elapsed;
                }
            }

            return seconds;
        }
    }
}
