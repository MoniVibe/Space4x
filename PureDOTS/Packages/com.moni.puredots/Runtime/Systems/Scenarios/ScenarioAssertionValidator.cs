using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Scenarios
{
    /// <summary>
    /// System that validates scenario assertions at the end of execution.
    /// Checks metrics against expected values and reports failures.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct ScenarioAssertionValidator : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Only validate once at the end of scenario execution
            if (!SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                return;
            }

            // Check if scenario is complete (this system runs after scenario completes)
            // For now, we'll validate assertions when this system runs
            // In a full implementation, this would be called from ScenarioRunnerExecutor after execution

            // This is a placeholder - actual validation happens in ScenarioRunnerExecutor
            // after scenario execution completes
        }

        /// <summary>
        /// Validates all assertions against collected metrics.
        /// Called by ScenarioRunnerExecutor after scenario execution.
        /// </summary>
        public static void ValidateAssertions(
            in NativeList<ScenarioAssertion> assertions,
            in NativeHashMap<FixedString64Bytes, double> metrics,
            ref NativeList<ScenarioAssertionResult> results)
        {
            results.Clear();

            for (int i = 0; i < assertions.Length; i++)
            {
                var assertion = assertions[i];
                var metricId = assertion.MetricId;

                if (!metrics.TryGetValue(metricId, out var actualValue))
                {
                    // Metric not found - assertion fails
                    results.Add(new ScenarioAssertionResult
                    {
                        MetricId = metricId,
                        Passed = false,
                        ActualValue = 0.0,
                        ExpectedValue = assertion.ExpectedValue,
                        Operator = assertion.Operator,
                        FailureMessage = new FixedString128Bytes($"Metric '{metricId}' not found")
                    });
                    continue;
                }

                var result = ScenarioAssertionEvaluator.Validate(assertion, actualValue);
                results.Add(result);
            }
        }
    }
}



