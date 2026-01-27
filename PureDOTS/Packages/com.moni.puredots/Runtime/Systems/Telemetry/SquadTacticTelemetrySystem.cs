using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Telemetry
{
    /// <summary>
    /// Emits lightweight metrics + events for squad tactic orders so HUDs/headless runs
    /// can observe combat intent churn without instrumenting the main loop.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(CommsTelemetrySystem))]
    [UpdateBefore(typeof(TelemetryExportSystem))]
    public partial struct SquadTacticTelemetrySystem : ISystem
    {
        private static readonly FixedString64Bytes MetricActive = "tactic.orders.active";
        private static readonly FixedString64Bytes MetricIssued = "tactic.orders.issued";
        private static readonly FixedString64Bytes MetricAckRequired = "tactic.orders.ack_required";
        private static readonly FixedString64Bytes MetricAckOptional = "tactic.orders.ack_optional";

        private static readonly FixedString64Bytes EventType = "ai.tactic.order";
        private static readonly FixedString64Bytes EventSource = "squad.tactic";

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<SquadCohesionState>();
            state.RequireForUpdate<SquadTacticOrder>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var exportConfig)
                || exportConfig.Enabled == 0
                || (exportConfig.Loops & TelemetryLoopFlags.Combat) == 0)
            {
                return;
            }

            var emitMetrics = (exportConfig.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) != 0;
            var emitEvents = (exportConfig.Flags & TelemetryExportFlags.IncludeTelemetryEvents) != 0;

            DynamicBuffer<TelemetryMetric> metricBuffer = default;
            if (emitMetrics && SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity)
                && state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                metricBuffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            }
            else
            {
                emitMetrics = false;
            }

            DynamicBuffer<TelemetryEvent> eventBuffer = default;
            var eventsRemaining = exportConfig.MaxEventsPerTick == 0 ? 64 : exportConfig.MaxEventsPerTick;
            if (emitEvents
                && SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var streamRef)
                && streamRef.Stream != Entity.Null
                && state.EntityManager.HasBuffer<TelemetryEvent>(streamRef.Stream))
            {
                eventBuffer = state.EntityManager.GetBuffer<TelemetryEvent>(streamRef.Stream);
            }
            else
            {
                emitEvents = false;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var active = 0;
            var issued = 0;
            var ackRequired = 0;
            var ackOptional = 0;

            foreach (var (tactic, cohesion, entity) in SystemAPI.Query<
                         RefRO<SquadTacticOrder>,
                         RefRW<SquadCohesionState>>()
                         .WithEntityAccess())
            {
                if (tactic.ValueRO.Kind == SquadTacticKind.None)
                {
                    continue;
                }

                active++;
                if (tactic.ValueRO.AckMode != 0)
                {
                    ackRequired++;
                }
                else
                {
                    ackOptional++;
                }

                if (tactic.ValueRO.IssueTick == 0 || cohesion.ValueRO.LastTelemetryTick == tactic.ValueRO.IssueTick)
                {
                    continue;
                }

                issued++;
                if (emitEvents && eventsRemaining > 0)
                {
                    var payload = BuildPayload(entity, tactic.ValueRO);
                    eventBuffer.AddEvent(EventType, time.Tick, EventSource, payload);
                    eventsRemaining--;
                }
                else if (eventsRemaining == 0)
                {
                    emitEvents = false;
                }

                var writable = cohesion.ValueRW;
                writable.LastTelemetryTick = tactic.ValueRO.IssueTick;
                cohesion.ValueRW = writable;
            }

            if (emitMetrics)
            {
                metricBuffer.AddMetric(MetricActive, active, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric(MetricIssued, issued, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric(MetricAckRequired, ackRequired, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric(MetricAckOptional, ackOptional, TelemetryMetricUnit.Count);
            }
        }

        private static FixedString128Bytes BuildPayload(Entity entity, in SquadTacticOrder tactic)
        {
            var payload = new FixedString128Bytes();
            payload.Append('{');
            payload.Append('"');
            payload.Append('e');
            payload.Append('"');
            payload.Append(':');
            payload.Append(entity.Index);
            payload.Append(',');
            payload.Append('"');
            payload.Append('k');
            payload.Append('"');
            payload.Append(':');
            payload.Append((int)tactic.Kind);
            payload.Append(',');
            payload.Append('"');
            payload.Append('a');
            payload.Append('"');
            payload.Append(':');
            payload.Append(tactic.AckMode);
            payload.Append(',');
            payload.Append('"');
            payload.Append('f');
            payload.Append('"');
            payload.Append(':');
            payload.Append(tactic.FocusBudgetCost);
            if (tactic.Target != Entity.Null)
            {
                payload.Append(',');
                payload.Append('"');
                payload.Append('t');
                payload.Append('"');
                payload.Append(':');
                payload.Append(tactic.Target.Index);
            }
            payload.Append('}');
            return payload;
        }
    }
}


