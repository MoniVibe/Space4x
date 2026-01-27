using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Snapshot of pooling metrics published for diagnostics and tooling overlays.
    /// </summary>
    public struct PoolingDiagnostics : IComponentData
    {
        public int NativeListsBorrowed;
        public int NativeListsAvailable;
        public int NativeQueuesBorrowed;
        public int NativeQueuesAvailable;
        public int CommandBuffersBorrowed;
        public int CommandBuffersAvailable;
        public int EntityPoolCount;
        public int EntityInstancesAvailable;
        public int EntityInstancesBorrowed;
        public int PendingDisposals;
    }
}

