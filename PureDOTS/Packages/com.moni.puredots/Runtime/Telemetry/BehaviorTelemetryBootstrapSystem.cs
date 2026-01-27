using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// Ensures telemetry config + buffers exist in every gameplay world.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BehaviorTelemetryBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<BehaviorTelemetryConfig>())
            {
                var configEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(configEntity, new BehaviorTelemetryConfig
                {
                    AggregateCadenceTicks = 30
                });
            }

            if (!SystemAPI.HasSingleton<BehaviorTelemetryState>())
            {
                var telemetryEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<BehaviorTelemetryState>(telemetryEntity);
                var buffer = state.EntityManager.AddBuffer<BehaviorTelemetryRecord>(telemetryEntity);
                buffer.Clear();
            }

            state.Enabled = false;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }
}
