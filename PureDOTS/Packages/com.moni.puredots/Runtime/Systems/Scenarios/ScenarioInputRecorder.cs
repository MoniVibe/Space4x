using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace PureDOTS.Systems.Scenarios
{
    /// <summary>
    /// System that records agent decisions and player inputs each tick for deterministic replay validation.
    /// Logs decisions to ScenarioInputLog buffer on scenario entity.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(SimulationSystemGroup))]
    public partial struct ScenarioInputRecorder : ISystem
    {
        private EntityQuery _scenarioQuery;
        private EntityQuery _agentDecisionQuery;

        public void OnCreate(ref SystemState state)
        {
            _scenarioQuery = state.GetEntityQuery(ComponentType.ReadWrite<ScenarioInfo>());
            _agentDecisionQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<EntityIntent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_scenarioQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                return;
            }

            var scenarioEntity = _scenarioQuery.GetSingletonEntity();
            if (!state.EntityManager.HasBuffer<ScenarioInputLog>(scenarioEntity))
            {
                state.EntityManager.AddBuffer<ScenarioInputLog>(scenarioEntity);
            }

            var logBuffer = state.EntityManager.GetBuffer<ScenarioInputLog>(scenarioEntity);
            var currentTick = timeState.Tick;

            // Record agent decisions (entities with EntityIntent)
            // Phase 0: Basic recording of intent changes
            // Phase 2: Will record GOAP/Utility decisions in detail
            foreach (var (intent, transform, entity) in SystemAPI.Query<RefRO<EntityIntent>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (intent.ValueRO.IsValid == 0)
                {
                    continue;
                }

                // Only log if intent changed this tick (to avoid spam)
                // For Phase 0, we log all valid intents each tick
                // In Phase 2, we'll track previous intent to detect changes

                var agentId = new FixedString64Bytes($"entity_{entity.Index}");
                var decisionType = new FixedString64Bytes("intent");
                var decisionData = new FixedString128Bytes($"mode={intent.ValueRO.Mode} target={intent.ValueRO.TargetEntity.Index}");

                var entry = ScenarioInputLogHelper.CreateEntry(currentTick, agentId, decisionType, decisionData);
                logBuffer.Add(new ScenarioInputLog { Entry = entry });
            }

            // Record player/hand inputs (if any)
            // This would be extended to capture actual player input events
            // For Phase 0, this is a placeholder
        }
    }
}



