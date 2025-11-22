using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Publishes mining telemetry metrics for debug and HUD bindings without direct UI references.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XRegistryTelemetrySystem))]
    public partial struct Space4XMiningTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryQuery;
        private EntityQuery _miningTelemetryQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<Space4XMiningTelemetry>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _miningTelemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XMiningTelemetry>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var miningTelemetry = _miningTelemetryQuery.GetSingleton<Space4XMiningTelemetry>();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            buffer.AddMetric("space4x.mining.oreInHold", miningTelemetry.OreInHold, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.mining.oreInHold.lastTick", miningTelemetry.LastUpdateTick);
        }
    }
}
