using Unity.Collections;
using Unity.Entities;

namespace Space4X.Demo
{
    /// <summary>
    /// Component for tracking demo report data (game name, scenario, timing).
    /// </summary>
    public struct DemoReportData : IComponentData
    {
        public FixedString64Bytes GameName;
        public FixedString64Bytes ScenarioName;
        public uint StartTick;
        public uint EndTick;
    }

    /// <summary>
    /// Singleton component for demo reporter state.
    /// </summary>
    public struct DemoReporterState : IComponentData
    {
        public byte ReportStarted;
        public byte ReportCompleted;
    }
}

