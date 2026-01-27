using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// Runtime state for telemetry export writers so multiple systems coordinate caps.
    /// </summary>
    public struct TelemetryExportState : IComponentData
    {
        public FixedString128Bytes RunId;
        public ulong BytesWritten;
        public ulong MaxOutputBytes;
        public byte CapReached;
    }
}
