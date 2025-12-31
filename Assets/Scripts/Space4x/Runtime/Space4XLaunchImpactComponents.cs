using Unity.Entities;

namespace Space4X.Runtime
{
    /// <summary>
    /// Singleton stream entity for launch impact requests.
    /// </summary>
    public struct Space4XLaunchRequestStream : IComponentData { }

    [InternalBufferCapacity(8)]
    public struct Space4XCargoDeliveryRequest : IBufferElementData
    {
        public Entity SourceLauncher;
        public Entity Payload;
        public Entity Target;
        public uint Tick;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XProbeActivateRequest : IBufferElementData
    {
        public Entity SourceLauncher;
        public Entity Payload;
        public Entity Target;
        public uint Tick;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XCrewTransferRequest : IBufferElementData
    {
        public Entity SourceLauncher;
        public Entity Payload;
        public Entity Target;
        public uint Tick;
    }
}
