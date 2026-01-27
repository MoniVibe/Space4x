using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Requests a deferred process exit so telemetry exporters can flush before quitting.
    /// </summary>
    public struct HeadlessExitRequest : IComponentData
    {
        public int ExitCode;
        public uint RequestedTick;
    }
}

