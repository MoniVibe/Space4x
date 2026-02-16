using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Telemetry.TelemetryExportBootstrapSystem))]
    public partial struct Space4XHeadlessTelemetryConfigSystem : ISystem
    {
        private byte _applied;

        public void OnCreate(ref SystemState state)
        {
            RuntimeMode.RefreshFromEnvironment();
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            Space4XHeadlessDiagnostics.InitializeFromArgs();
            if (!Space4XHeadlessDiagnostics.Enabled)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TelemetryExportConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_applied != 0)
            {
                return;
            }

            var config = SystemAPI.GetSingletonRW<TelemetryExportConfig>();
            if (!Space4XHeadlessDiagnostics.TelemetryEnabled)
            {
                config.ValueRW.Enabled = 0;
                config.ValueRW.OutputPath = default;
                config.ValueRW.Version++;
                _applied = 1;
                return;
            }

            // Headless validations depend on PureDOTS telemetry metrics being exported.
            config.ValueRW.Flags |= TelemetryExportFlags.IncludeTelemetryMetrics;

            if (!string.IsNullOrWhiteSpace(Space4XHeadlessDiagnostics.TelemetryPath))
            {
                config.ValueRW.OutputPath = new FixedString512Bytes(Space4XHeadlessDiagnostics.TelemetryPath);
                config.ValueRW.Enabled = 1;
                if (config.ValueRW.MaxOutputBytes == 0)
                {
                    config.ValueRW.MaxOutputBytes = 24u * 1024u * 1024u;
                }
                config.ValueRW.Version++;
            }

            // Some metric emitters expect the TelemetryStream entity to already have a TelemetryMetric buffer.
            if (SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity) &&
                telemetryEntity != Entity.Null &&
                !state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                state.EntityManager.AddBuffer<TelemetryMetric>(telemetryEntity);
            }

            _applied = 1;
        }
    }
}
