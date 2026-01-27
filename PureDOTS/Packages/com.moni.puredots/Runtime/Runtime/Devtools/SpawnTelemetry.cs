#if DEVTOOLS_ENABLED
using Unity.Entities;

namespace PureDOTS.Runtime.Devtools
{
    /// <summary>
    /// Per-tick spawn telemetry singleton.
    /// </summary>
    public struct SpawnTelemetry : IComponentData
    {
        public int RequestsThisTick;
        public int ValidatedThisTick;
        public int SpawnedThisTick;
        public int FailuresThisTick;
        public float AvgValidatorTimeMs;
        public int FailuresByReason_TooSteep;
        public int FailuresByReason_Overlap;
        public int FailuresByReason_OutOfBounds;
        public int FailuresByReason_ForbiddenVolume;
        public int FailuresByReason_NotOnNavmesh;
    }
}
#endif























