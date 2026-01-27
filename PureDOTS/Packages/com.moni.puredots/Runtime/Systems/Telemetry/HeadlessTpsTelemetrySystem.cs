using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems.Telemetry
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(TelemetryExportSystem))]
    public partial struct HeadlessTpsTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var exportConfig) ||
                exportConfig.Enabled == 0 ||
                (exportConfig.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickState))
            {
                return;
            }

            var cadence = exportConfig.CadenceTicks > 0 ? exportConfig.CadenceTicks : 30u;
            if (cadence > 1u && tickState.Tick % cadence != 0u)
            {
                return;
            }

            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            var entityManager = state.EntityManager;
            var now = UnityEngine.Time.realtimeSinceStartupAsDouble;

            if (!entityManager.HasComponent<HeadlessTpsTelemetryState>(telemetryEntity))
            {
                entityManager.AddComponentData(telemetryEntity, new HeadlessTpsTelemetryState
                {
                    LastRealTime = now,
                    LastTick = tickState.Tick
                });
                return;
            }

            var sampleState = entityManager.GetComponentData<HeadlessTpsTelemetryState>(telemetryEntity);
            var deltaReal = now - sampleState.LastRealTime;
            var tickDelta = tickState.Tick - sampleState.LastTick;

            sampleState.LastRealTime = now;
            sampleState.LastTick = tickState.Tick;
            entityManager.SetComponentData(telemetryEntity, sampleState);

            if (deltaReal <= 0d || tickDelta == 0u)
            {
                return;
            }

            if (!entityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                entityManager.AddBuffer<TelemetryMetric>(telemetryEntity);
            }

            var metrics = entityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            var tpsReal = (float)(tickDelta / deltaReal);
            metrics.AddMetric("sim.tps_real", tpsReal, TelemetryMetricUnit.Custom);

            if (SystemAPI.TryGetSingleton<HeadlessTpsCap>(out var cap) && cap.TargetTps > 0f)
            {
                metrics.AddMetric("sim.cap_target_tps", cap.TargetTps, TelemetryMetricUnit.Custom);
            }
        }
    }

    public struct HeadlessTpsTelemetryState : IComponentData
    {
        public double LastRealTime;
        public uint LastTick;
    }
}
