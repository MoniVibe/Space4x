using PureDOTS.Runtime.Scenarios;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Scenarios
{
    /// <summary>
    /// Bootstrap system that resolves the scenario entity and stores it in ScenarioEntitySingleton
    /// for Burst-compatible access by ScenarioMetricsUtility.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct ScenarioEntityBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Resolve scenario entity
            var scenarioQuery = SystemAPI.QueryBuilder()
                .WithAll<ScenarioInfo>()
                .Build();
            
            if (scenarioQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var scenarioEntity = scenarioQuery.GetSingletonEntity();

            // Ensure metrics buffer exists on scenario entity (required for Burst-compatible access)
            if (!state.EntityManager.HasBuffer<ScenarioMetricSample>(scenarioEntity))
            {
                state.EntityManager.AddBuffer<ScenarioMetricSample>(scenarioEntity);
            }

            // Ensure singleton exists and is set
            if (!SystemAPI.HasSingleton<ScenarioEntitySingleton>())
            {
                var singletonEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(singletonEntity, new ScenarioEntitySingleton
                {
                    Value = scenarioEntity
                });
            }
            else
            {
                var singletonEntity = SystemAPI.GetSingletonEntity<ScenarioEntitySingleton>();
                state.EntityManager.SetComponentData(singletonEntity, new ScenarioEntitySingleton
                {
                    Value = scenarioEntity
                });
            }

            // Disable after first update
            state.Enabled = false;
        }
    }
}

