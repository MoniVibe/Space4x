using System;
using Unity.Collections;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// Types of metrics that can be tracked and asserted in scenarios.
    /// </summary>
    public enum ScenarioMetricType : byte
    {
        Count = 0,          // Integer count (e.g., entity count, event count)
        Threshold = 1,      // Float threshold (e.g., resource amount, health)
        Range = 2,          // Float range (min <= value <= max)
        Boolean = 3,        // Boolean condition (true/false)
        NeverNaN = 4,      // Special: value must never be NaN
        AlwaysPositive = 5  // Special: value must always be >= 0
    }

    /// <summary>
    /// Comparison operators for assertions.
    /// </summary>
    public enum ScenarioAssertionOperator : byte
    {
        Equal = 0,              // ==
        NotEqual = 1,           // !=
        LessThan = 2,           // <
        LessThanOrEqual = 3,    // <=
        GreaterThan = 4,        // >
        GreaterThanOrEqual = 5, // >=
        Always = 6,             // Always true (for boolean metrics)
        Never = 7                // Never true (for boolean metrics)
    }

    /// <summary>
    /// A metric definition that can be tracked during scenario execution.
    /// </summary>
    [Serializable]
    public class ScenarioMetricDefinition
    {
        public string metricId = string.Empty;
        public ScenarioMetricType type = ScenarioMetricType.Count;
        public string description = string.Empty;
    }

    /// <summary>
    /// Native representation of a metric definition.
    /// </summary>
    public struct ScenarioMetricDef
    {
        public FixedString64Bytes MetricId;
        public ScenarioMetricType Type;
        public FixedString128Bytes Description;
    }

    /// <summary>
    /// A tracked metric value at a specific point in time.
    /// </summary>
    public struct ScenarioMetricValue
    {
        public FixedString64Bytes MetricId;
        public double Value;
        public uint Tick;
    }
}



