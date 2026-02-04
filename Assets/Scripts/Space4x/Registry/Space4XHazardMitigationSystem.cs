using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Reduces incoming hazard damage using HazardResistance buffers and reports mitigated amounts to telemetry.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XHazardMitigationSystem : ISystem
    {
        private static readonly FixedString64Bytes MetricHazardMitigated = "space4x.hazard.mitigated";

        private EntityQuery _hazardQuery;
        private EntityQuery _telemetryQuery;
        private BufferLookup<HazardResistance> _resistanceLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _hazardQuery = SystemAPI.QueryBuilder()
                .WithAllRW<HazardDamageEvent>()
                .Build();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _resistanceLookup = state.GetBufferLookup<HazardResistance>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_hazardQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            _resistanceLookup.Update(ref state);

            var hasTelemetry = !_telemetryQuery.IsEmptyIgnoreFilter;
            var mitigatedTotal = 0f;
            DynamicBuffer<TelemetryMetric> telemetryBuffer = default;
            if (hasTelemetry)
            {
                var telemetryEntity = _telemetryQuery.GetSingletonEntity();
                telemetryBuffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            }

            foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<HazardDamageEvent>>().WithEntityAccess())
            {
                var eventsBuffer = events;
                var resistance = GetResistance(entity);
                for (var i = 0; i < eventsBuffer.Length; i++)
                {
                    var evt = eventsBuffer[i];
                    var reduced = ApplyResistance(evt.Amount, resistance);
                    mitigatedTotal += math.max(0f, evt.Amount - reduced);
                    evt.Amount = reduced;
                    eventsBuffer[i] = evt;
                }
            }

            if (hasTelemetry && telemetryBuffer.IsCreated && mitigatedTotal > 0f)
            {
                telemetryBuffer.AddMetric(MetricHazardMitigated, mitigatedTotal, TelemetryMetricUnit.Custom);
            }
        }

        private float GetResistance(Entity entity)
        {
            if (!_resistanceLookup.HasBuffer(entity))
            {
                return 0f;
            }

            var buffer = _resistanceLookup[entity];
            var best = 0f;
            for (var i = 0; i < buffer.Length; i++)
            {
                best = math.max(best, buffer[i].ResistanceMultiplier);
            }

            return math.saturate(best);
        }

        private static float ApplyResistance(float amount, float resistance)
        {
            if (amount <= 0f || resistance <= 0f)
            {
                return amount;
            }

            return amount * (1f - math.saturate(resistance));
        }
    }
}
