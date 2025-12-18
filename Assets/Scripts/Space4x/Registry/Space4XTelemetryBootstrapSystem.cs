using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures the TelemetryStream singleton exists with its TelemetryMetric buffer.
    /// This allows telemetry systems to publish metrics without requiring manual setup in demo scenes.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XTelemetryBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity))
            {
                telemetryEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(telemetryEntity, new TelemetryStream
                {
                    Version = 0,
                    LastTick = 0
                });
            }

            if (!entityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                entityManager.AddBuffer<TelemetryMetric>(telemetryEntity);
            }

            TelemetryStreamUtility.EnsureEventStream(entityManager);

            state.Enabled = false;
        }
    }
}
