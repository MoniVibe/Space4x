using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Space4X.Telemetry;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Emits telemetry events for hostile control override start/end transitions.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Telemetry.TelemetryExportSystem))]
    public partial struct Space4XControlOverrideTelemetrySystem : ISystem
    {
        private FixedString64Bytes _sourceId;
        private FixedString64Bytes _eventOverrideStarted;
        private FixedString64Bytes _eventOverrideEnded;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HostileControlOverride>();
            state.RequireForUpdate<TelemetryExportConfig>();
            state.RequireForUpdate<TelemetryStreamSingleton>();
            state.RequireForUpdate<TimeState>();

            _sourceId = new FixedString64Bytes("Space4X.Agency");
            _eventOverrideStarted = new FixedString64Bytes("ControlOverrideStarted");
            _eventOverrideEnded = new FixedString64Bytes("ControlOverrideEnded");
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryEvents) == 0)
            {
                return;
            }

            if (!TryGetEventBuffer(ref state, out var eventBuffer))
            {
                return;
            }

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            foreach (var (overrideRef, entity) in SystemAPI.Query<RefRW<HostileControlOverride>>().WithEntityAccess())
            {
                var active = overrideRef.ValueRO.Active != 0;
                var reported = overrideRef.ValueRO.LastReportedActive != 0;

                if (active && !reported)
                {
                    eventBuffer.AddEvent(_eventOverrideStarted, tick, _sourceId, BuildPayload(entity, overrideRef.ValueRO));
                    overrideRef.ValueRW.LastReportedActive = 1;
                }
                else if (!active && reported)
                {
                    eventBuffer.AddEvent(_eventOverrideEnded, tick, _sourceId, BuildPayload(entity, overrideRef.ValueRO));
                    overrideRef.ValueRW.LastReportedActive = 0;
                }
            }
        }

        private static bool TryGetEventBuffer(ref SystemState state, out DynamicBuffer<TelemetryEvent> buffer)
        {
            buffer = default;
            using var query = state.GetEntityQuery(ComponentType.ReadOnly<TelemetryStreamSingleton>());
            if (!query.TryGetSingleton(out TelemetryStreamSingleton telemetryRef))
            {
                return false;
            }

            if (telemetryRef.Stream == Entity.Null || !state.EntityManager.HasBuffer<TelemetryEvent>(telemetryRef.Stream))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryEvent>(telemetryRef.Stream);
            return true;
        }

        private static FixedString128Bytes BuildPayload(Entity entity, in HostileControlOverride data)
        {
            var writer = new TelemetryJsonWriter();
            writer.AddEntity("entity", entity);
            writer.AddEntity("controller", data.Controller);
            writer.AddString("domains", data.Domains.ToString());
            writer.AddString("reason", data.Reason.ToString());
            writer.AddUInt("establishedTick", data.EstablishedTick);
            writer.AddUInt("expireTick", data.ExpireTick);
            writer.AddFloat("pressure", data.Pressure);
            writer.AddFloat("legitimacy", data.Legitimacy);
            writer.AddFloat("hostility", data.Hostility);
            writer.AddFloat("consent", data.Consent);
            return writer.Build();
        }
    }
}
