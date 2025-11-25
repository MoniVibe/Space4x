using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures the TelemetryStream singleton exists with its TelemetryMetric buffer.
    /// This allows telemetry systems to publish metrics without requiring manual setup in demo scenes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XTelemetryBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Check if TelemetryStream already exists
            if (SystemAPI.TryGetSingletonEntity<TelemetryStream>(out _))
            {
                // Already exists, disable system
                state.Enabled = false;
                return;
            }

            // Create TelemetryStream singleton entity
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<TelemetryStream>(entity);
            state.EntityManager.AddBuffer<TelemetryMetric>(entity);

            // Disable system after creation
            state.Enabled = false;
        }
    }
}

