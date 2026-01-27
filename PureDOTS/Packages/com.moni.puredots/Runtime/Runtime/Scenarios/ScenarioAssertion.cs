using System;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// An assertion that validates a metric against an expected value or condition.
    /// Assertions are checked at the end of scenario execution.
    /// </summary>
    [Serializable]
    public class ScenarioAssertionData
    {
        public string metricId = string.Empty;
        public ScenarioAssertionOperator op = ScenarioAssertionOperator.GreaterThanOrEqual;
        public double expectedValue = 0.0;
        public string description = string.Empty;
    }

    /// <summary>
    /// Native representation of an assertion.
    /// </summary>
    public struct ScenarioAssertion
    {
        public FixedString64Bytes MetricId;
        public ScenarioAssertionOperator Operator;
        public double ExpectedValue;
        public FixedString128Bytes Description;
    }

    /// <summary>
    /// Result of validating an assertion.
    /// </summary>
    public struct ScenarioAssertionResult
    {
        public FixedString64Bytes MetricId;
        public bool Passed;
        public double ActualValue;
        public double ExpectedValue;
        public ScenarioAssertionOperator Operator;
        public FixedString128Bytes FailureMessage;
    }

    /// <summary>
    /// Helper methods for evaluating assertions.
    /// </summary>
    public static class ScenarioAssertionEvaluator
    {
        public static bool Evaluate(ScenarioAssertionOperator op, double actual, double expected)
        {
            return op switch
            {
                ScenarioAssertionOperator.Equal => math.abs(actual - expected) < 0.0001,
                ScenarioAssertionOperator.NotEqual => math.abs(actual - expected) >= 0.0001,
                ScenarioAssertionOperator.LessThan => actual < expected,
                ScenarioAssertionOperator.LessThanOrEqual => actual <= expected,
                ScenarioAssertionOperator.GreaterThan => actual > expected,
                ScenarioAssertionOperator.GreaterThanOrEqual => actual >= expected,
                ScenarioAssertionOperator.Always => true,
                ScenarioAssertionOperator.Never => false,
                _ => false
            };
        }

        public static ScenarioAssertionResult Validate(ScenarioAssertion assertion, double actualValue)
        {
            var passed = Evaluate(assertion.Operator, actualValue, assertion.ExpectedValue);
            var result = new ScenarioAssertionResult
            {
                MetricId = assertion.MetricId,
                Passed = passed,
                ActualValue = actualValue,
                ExpectedValue = assertion.ExpectedValue,
                Operator = assertion.Operator,
                FailureMessage = default
            };

            if (!passed)
            {
                result.FailureMessage = new FixedString128Bytes(
                    $"Assertion failed: {assertion.MetricId} {GetOperatorString(assertion.Operator)} {assertion.ExpectedValue}, actual={actualValue}"
                );
            }

            return result;
        }

        private static string GetOperatorString(ScenarioAssertionOperator op)
        {
            return op switch
            {
                ScenarioAssertionOperator.Equal => "==",
                ScenarioAssertionOperator.NotEqual => "!=",
                ScenarioAssertionOperator.LessThan => "<",
                ScenarioAssertionOperator.LessThanOrEqual => "<=",
                ScenarioAssertionOperator.GreaterThan => ">",
                ScenarioAssertionOperator.GreaterThanOrEqual => ">=",
                ScenarioAssertionOperator.Always => "always",
                ScenarioAssertionOperator.Never => "never",
                _ => "?"
            };
        }
    }
}

