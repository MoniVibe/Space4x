using System;
using System.Diagnostics;
using PureDOTS.Runtime.Telemetry;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Identifiers for manual phase groups that can be toggled at runtime.
    /// </summary>
    public enum ManualPhaseGroupId : byte
    {
        Camera = 0,
        Transport = 1,
        History = 2
    }

    /// <summary>
    /// Base class for manual phase groups that exposes common instrumentation.
    /// </summary>
    public abstract partial class ManualPhaseSystemGroup : ComponentSystemGroup
    {
        private readonly ManualPhaseGroupId _phaseId;
        private readonly FrameTimingGroup _timingGroup;
        private Stopwatch _stopwatch;

        protected ManualPhaseSystemGroup(ManualPhaseGroupId phaseId, FrameTimingGroup timingGroup)
        {
            _phaseId = phaseId;
            _timingGroup = timingGroup;
        }

        public ManualPhaseGroupId PhaseId => _phaseId;
        public FrameTimingGroup TimingGroup => _timingGroup;

        protected override void OnCreate()
        {
            base.OnCreate();
            _stopwatch = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            _stopwatch.Restart();
            base.OnUpdate();
            _stopwatch.Stop();
            SystemGroupInstrumentationUtility.Record(this, _stopwatch, _timingGroup);
        }
    }

    /// <summary>
    /// Processes camera input and state synchronisation ahead of simulation.
    /// </summary>
    [UpdateInGroup(typeof(CameraInputSystemGroup))]
    public sealed partial class CameraPhaseGroup : ManualPhaseSystemGroup
    {
        public CameraPhaseGroup() : base(ManualPhaseGroupId.Camera, FrameTimingGroup.Camera) { }
    }

    /// <summary>
    /// Transport/logistics phase that runs after spatial updates but before general gameplay systems.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(GameplaySystemGroup))]
    public sealed partial class TransportPhaseGroup : ManualPhaseSystemGroup
    {
        public TransportPhaseGroup() : base(ManualPhaseGroupId.Transport, FrameTimingGroup.Transport) { }
    }

    /// <summary>
    /// History capture phase that can be toggled without impacting the rest of late simulation.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public sealed partial class HistoryPhaseGroup : ManualPhaseSystemGroup
    {
        public HistoryPhaseGroup() : base(ManualPhaseGroupId.History, FrameTimingGroup.History) { }

        private static bool? s_disableHistory;

        protected override void OnUpdate()
        {
            if (IsHistoryDisabled())
            {
                return;
            }

            base.OnUpdate();
        }

        private static bool IsHistoryDisabled()
        {
            if (s_disableHistory.HasValue)
            {
                return s_disableHistory.Value;
            }

            var raw = global::System.Environment.GetEnvironmentVariable("PUREDOTS_HEADLESS_DISABLE_HISTORY");
            if (IsTruthy(raw))
            {
                s_disableHistory = true;
                return true;
            }

            raw = global::System.Environment.GetEnvironmentVariable("PUREDOTS_DISABLE_HISTORY");
            s_disableHistory = IsTruthy(raw);
            return s_disableHistory.Value;
        }

        private static bool IsTruthy(string raw)
        {
            return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}

