using Unity.Collections;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// Shared loop identifiers for headless proofs and telemetry.
    /// </summary>
    public static class TelemetryLoopIds
    {
        public static readonly FixedString64Bytes Extract = new FixedString64Bytes("extract");
        public static readonly FixedString64Bytes Logistics = new FixedString64Bytes("logistics");
        public static readonly FixedString64Bytes Construction = new FixedString64Bytes("construction");
        public static readonly FixedString64Bytes Exploration = new FixedString64Bytes("exploration");
        public static readonly FixedString64Bytes Combat = new FixedString64Bytes("combat");
        public static readonly FixedString64Bytes Rewind = new FixedString64Bytes("rewind");
        public static readonly FixedString64Bytes Time = new FixedString64Bytes("time");
    }
}
