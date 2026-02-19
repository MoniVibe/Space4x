using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Telemetry;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Publishes aggregated profile action metrics for headless validation.
    /// </summary>
    [UpdateInGroup(typeof(PureDOTS.Systems.LateSimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Profile.ProfileMutationSystem))]
    public partial struct Space4XProfileMutationTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryQuery;
        private EntityQuery _pendingQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TelemetryExportConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ProfileActionEventStream>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();
            _pendingQuery = SystemAPI.QueryBuilder()
                .WithAll<ProfileMutationPending>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var cadence = config.CadenceTicks > 0 ? config.CadenceTicks : 30u;
            if (tick % cadence != 0)
            {
                return;
            }

            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var metricBuffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            var streamEntity = SystemAPI.GetSingletonEntity<ProfileActionEventStream>();
            var actionBuffer = SystemAPI.GetBuffer<ProfileActionEvent>(streamEntity);

            var total = actionBuffer.Length;
            var obey = 0;
            var disobey = 0;
            var mining = 0;
            var issued = 0;

            for (int i = 0; i < actionBuffer.Length; i++)
            {
                switch (actionBuffer[i].Token)
                {
                    case ProfileActionToken.ObeyOrder:
                        obey++;
                        break;
                    case ProfileActionToken.DisobeyOrder:
                        disobey++;
                        break;
                    case ProfileActionToken.MineResource:
                        mining++;
                        break;
                    case ProfileActionToken.OrderIssued:
                        issued++;
                        break;
                }
            }

            var pending = _pendingQuery.CalculateEntityCount();

            metricBuffer.AddMetric("space4x.profile.actions.total", total, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.profile.actions.obey", obey, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.profile.actions.disobey", disobey, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.profile.actions.mining", mining, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.profile.actions.issued", issued, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.profile.pending", pending, TelemetryMetricUnit.Count);
        }
    }
}
