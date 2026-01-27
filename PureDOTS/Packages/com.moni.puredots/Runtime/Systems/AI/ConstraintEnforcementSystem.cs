using PureDOTS.Runtime.AI.Constraints;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Scenarios;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// System that validates actions against constraints.
    /// Prevents entities from taking forbidden actions even if they're optimal.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InterruptSystemGroup))]
    [UpdateBefore(typeof(GameplaySystemGroup))]
    public partial struct ConstraintEnforcementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Phase 2: Validate EntityIntent against constraints
            // If intent violates constraints, clear or modify it

            // Resolve scenario entity once for Burst-compatible metrics
            Entity scenarioEntity = Entity.Null;
            if (SystemAPI.TryGetSingleton<ScenarioEntitySingleton>(out var scenarioSingleton))
            {
                scenarioEntity = scenarioSingleton.Value;
            }
            else if (SystemAPI.HasSingleton<ScenarioInfo>())
            {
                scenarioEntity = SystemAPI.GetSingletonEntity<ScenarioInfo>();
            }

            // Get metric buffer lookup
            var metricLookup = SystemAPI.GetBufferLookup<ScenarioMetricSample>(isReadOnly: false);
            metricLookup.Update(ref state);
            
            foreach (var (intent, constraints, entity) in SystemAPI.Query<
                     RefRW<EntityIntent>,
                     DynamicBuffer<ActionConstraint>>()
                     .WithEntityAccess())
            {
                if (intent.ValueRO.IsValid == 0)
                {
                    continue;
                }

                // Check if current intent violates constraints
                var constraintsBuffer = constraints;
                var constraintViolated = false;

                // Check NoTrespass constraint (placeholder until zones are authored)
                if (ConstraintChecker.HasConstraint(constraintsBuffer, ConstraintType.NoTrespass))
                {
                    // Phase 3 will evaluate forbidden zones; for now mark violation if MoveTo outside scenario bounds.
                    constraintViolated |= intent.ValueRO.Mode == IntentMode.MoveTo;
                }

                // Check NoAttacking constraint
                if (ConstraintChecker.HasConstraint(constraintsBuffer, ConstraintType.NoAttacking) &&
                    intent.ValueRO.Mode == IntentMode.Attack)
                {
                    intent.ValueRW.IsValid = 0;
                    intent.ValueRW.Mode = IntentMode.Idle;
                    constraintViolated = true;
                }

                // Check NonLethal constraint (placeholder)
                if (ConstraintChecker.HasConstraint(constraintsBuffer, ConstraintType.NonLethal) &&
                    intent.ValueRO.Mode == IntentMode.Attack)
                {
                    intent.ValueRW.Mode = IntentMode.Flee;
                    constraintViolated = true;
                }

                if (constraintViolated && scenarioEntity != Entity.Null && metricLookup.HasBuffer(scenarioEntity))
                {
                    ScenarioMetricsUtility.SetMetric(ref metricLookup, scenarioEntity, "constraints.respected", 0.0);
                }
            }
        }
    }
}

