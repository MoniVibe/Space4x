using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// Tracks whether a performance budget violation has occurred during the current run.
    /// </summary>
    public struct PerformanceBudgetStatus : IComponentData
    {
        /// <summary>
        /// 0 when all budgets are respected, 1 once any metric exceeds its configured budget.
        /// </summary>
        public byte HasFailure;

        /// <summary>
        /// Metric identifier that triggered the failure (e.g., timing.fixedTick).
        /// </summary>
        public FixedString64Bytes Metric;

        /// <summary>
        /// Observed value for the failing metric.
        /// </summary>
        public float ObservedValue;

        /// <summary>
        /// Configured budget for the failing metric.
        /// </summary>
        public float BudgetValue;

        /// <summary>
        /// Simulation tick when the failure was recorded.
        /// </summary>
        public uint Tick;
    }
}
