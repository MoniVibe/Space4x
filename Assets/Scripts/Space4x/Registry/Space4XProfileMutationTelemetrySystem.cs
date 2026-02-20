using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Telemetry;
using Unity.Entities;
using Unity.Mathematics;

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
            var managedPilots = 0;
            var sumDirectiveAggression = 0f;
            var sumDirectiveCaution = 0f;
            var sumCommandTrust = 0f;
            var sumEffectiveAggression = 0f;
            var sumEffectiveCaution = 0f;
            var sumDirectivePressure = 0f;
            var sumTrustPressure = 0f;
            var sumSharedSelfPressure = 0f;
            var sumSharedKinPressure = 0f;
            var sumSharedLoyaltyPressure = 0f;
            var sharedPressureSamples = 0;
            var intentDriveLookup = SystemAPI.GetComponentLookup<VillagerIntentDrive>(true);
            intentDriveLookup.Update(ref state);

            foreach (var (directive, trust, runtime, entity) in SystemAPI.Query<
                         RefRO<Space4XPilotDirective>,
                         RefRO<Space4XPilotTrust>,
                         RefRO<Space4XPilotBehaviorRuntime>>()
                     .WithAll<Space4XPilotManagedTag>()
                     .WithEntityAccess())
            {
                managedPilots++;
                sumDirectiveAggression += (float)directive.ValueRO.AggressionBias;
                sumDirectiveCaution += (float)directive.ValueRO.CautionBias;
                sumCommandTrust += (float)trust.ValueRO.CommandTrust;
                sumEffectiveAggression += runtime.ValueRO.Effective.Aggression;
                sumEffectiveCaution += runtime.ValueRO.Effective.Caution;
                sumDirectivePressure += runtime.ValueRO.DirectivePressure;
                sumTrustPressure += runtime.ValueRO.TrustPressure;

                if (intentDriveLookup.HasComponent(entity))
                {
                    var drive = intentDriveLookup[entity];
                    sumSharedSelfPressure += drive.SelfPreservationPressure;
                    sumSharedKinPressure += drive.KinshipPressure;
                    sumSharedLoyaltyPressure += drive.LoyaltyPressure;
                    sharedPressureSamples++;
                }
            }

            var managedPilotDenom = math.max(1, managedPilots);
            var sharedPressureDenom = math.max(1, sharedPressureSamples);

            metricBuffer.AddMetric("space4x.profile.actions.total", total, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.profile.actions.obey", obey, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.profile.actions.disobey", disobey, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.profile.actions.mining", mining, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.profile.actions.issued", issued, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.profile.pending", pending, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.pilot.manager.count", managedPilots, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.pilot.manager.directive.aggression_bias_avg", sumDirectiveAggression / managedPilotDenom, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.pilot.manager.directive.caution_bias_avg", sumDirectiveCaution / managedPilotDenom, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.pilot.manager.trust.command_avg", sumCommandTrust / managedPilotDenom, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.pilot.manager.effective.aggression_avg", sumEffectiveAggression / managedPilotDenom, TelemetryMetricUnit.Ratio);
            metricBuffer.AddMetric("space4x.pilot.manager.effective.caution_avg", sumEffectiveCaution / managedPilotDenom, TelemetryMetricUnit.Ratio);
            metricBuffer.AddMetric("space4x.pilot.manager.pressure.directive_avg", sumDirectivePressure / managedPilotDenom, TelemetryMetricUnit.Ratio);
            metricBuffer.AddMetric("space4x.pilot.manager.pressure.trust_avg", sumTrustPressure / managedPilotDenom, TelemetryMetricUnit.Ratio);
            metricBuffer.AddMetric("space4x.pilot.manager.shared.self_pressure_avg", sumSharedSelfPressure / sharedPressureDenom, TelemetryMetricUnit.Ratio);
            metricBuffer.AddMetric("space4x.pilot.manager.shared.kin_pressure_avg", sumSharedKinPressure / sharedPressureDenom, TelemetryMetricUnit.Ratio);
            metricBuffer.AddMetric("space4x.pilot.manager.shared.loyalty_pressure_avg", sumSharedLoyaltyPressure / sharedPressureDenom, TelemetryMetricUnit.Ratio);
            metricBuffer.AddMetric("space4x.pilot.manager.shared.sample_count", sharedPressureSamples, TelemetryMetricUnit.Count);
        }
    }
}
