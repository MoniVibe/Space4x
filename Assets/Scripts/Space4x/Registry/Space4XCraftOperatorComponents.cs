using PureDOTS.Runtime.Agency;
using Unity.Entities;

namespace Space4X.Registry
{
    [InternalBufferCapacity(8)]
    public struct CraftOperatorAssignment : IBufferElementData
    {
        public AgencyDomain Domain;
        public Entity Controller;
        public ControlClaimSourceKind SourceKind;
        public byte Reserved0;
        public float Score;
        public uint UpdatedTick;
    }

    [InternalBufferCapacity(8)]
    public struct CraftOperatorConsole : IBufferElementData
    {
        public AgencyDomain Domain;
        public Entity Controller;
        public ControlClaimSourceKind SourceKind;
        public byte Reserved0;
        public float ConsoleQuality;
        public float DataLatencySeconds;
        public float DataFidelity;
        public uint UpdatedTick;
    }
}
