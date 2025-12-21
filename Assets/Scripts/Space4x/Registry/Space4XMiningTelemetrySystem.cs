using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Systems.AI;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Publishes mining telemetry metrics for debug and HUD bindings without direct UI references.
    /// </summary>
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(CarrierPickupSystem))]
    [UpdateAfter(typeof(VesselDepositSystem))]
    public partial struct Space4XMiningTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryQuery;
        private float _lastHeld;
        private uint _lastTick;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TelemetryExportConfig>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            // Truth-source metric for headless + training: derive ore directly from carrier storage.
            // (The Space4XMiningTelemetry singleton is a presentation cache and can be stale if the
            // aggregator pipeline is bypassed in headless.)
            var totalHeld = 0f;
            foreach (var storage in SystemAPI.Query<DynamicBuffer<ResourceStorage>>().WithAll<Carrier>())
            {
                for (var i = 0; i < storage.Length; i++)
                {
                    totalHeld += math.max(0f, storage[i].Amount);
                }
            }

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var cadence = config.CadenceTicks > 0 ? config.CadenceTicks : 30u;
            if (tick % cadence != 0)
            {
                return;
            }
            buffer.AddMetric("space4x.mining.oreInHold", totalHeld, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.mining.oreInHold.lastTick", tick);

            var activeWorkers = 0;
            foreach (var aiState in SystemAPI.Query<RefRO<VesselAIState>>().WithAll<MiningVessel>())
            {
                if (aiState.ValueRO.CurrentState == VesselAIState.State.Mining)
                {
                    activeWorkers++;
                }
            }

            var activeNodes = 0;
            foreach (var node in SystemAPI.Query<RefRO<ResourceSourceState>>())
            {
                if (node.ValueRO.UnitsRemaining > 0f)
                {
                    activeNodes++;
                }
            }

            var outputPerTick = 0f;
            if (_lastTick > 0 && tick > _lastTick)
            {
                var delta = math.max(0f, totalHeld - _lastHeld);
                outputPerTick = delta / (tick - _lastTick);
            }

            _lastHeld = totalHeld;
            _lastTick = tick;

            buffer.AddMetric("loop.extract.buffer", totalHeld, TelemetryMetricUnit.Custom);
            buffer.AddMetric("loop.extract.outputPerTick", outputPerTick, TelemetryMetricUnit.Custom);
            buffer.AddMetric("loop.extract.activeWorkers", activeWorkers, TelemetryMetricUnit.Count);
            buffer.AddMetric("loop.extract.nodes.active", activeNodes, TelemetryMetricUnit.Count);
        }
    }
}
