using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Applies scenario-driven behavior overrides after bootstrapping default configs.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(BehaviorConfigBootstrapSystem))]
    [UpdateAfter(typeof(PureDOTS.Runtime.Telemetry.BehaviorTelemetryBootstrapSystem))]
    public partial struct BehaviorScenarioOverrideSystem : ISystem
    {
        private EntityQuery _overrideQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _overrideQuery = state.GetEntityQuery(ComponentType.ReadOnly<BehaviorScenarioOverrideComponent>());
            state.RequireForUpdate<BehaviorConfigRegistry>();
            state.RequireForUpdate<PureDOTS.Runtime.Telemetry.BehaviorTelemetryConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_overrideQuery.IsEmptyIgnoreFilter)
            {
                state.Enabled = false;
                return;
            }

            var overrideEntity = _overrideQuery.GetSingletonEntity();
            var scenarioOverride = state.EntityManager.GetComponentData<BehaviorScenarioOverrideComponent>(overrideEntity).Value;

            var configRW = SystemAPI.GetSingletonRW<BehaviorConfigRegistry>();
            var telemetryRW = SystemAPI.GetSingletonRW<PureDOTS.Runtime.Telemetry.BehaviorTelemetryConfig>();

            var config = configRW.ValueRO;
            var telemetry = telemetryRW.ValueRO;
            BehaviorConfigOverrideUtility.Apply(ref config, ref telemetry, scenarioOverride);
            configRW.ValueRW = config;
            telemetryRW.ValueRW = telemetry;

            state.EntityManager.DestroyEntity(overrideEntity);
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }
}
