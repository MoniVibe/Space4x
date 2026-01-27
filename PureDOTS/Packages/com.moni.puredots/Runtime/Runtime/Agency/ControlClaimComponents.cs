using Unity.Entities;

namespace PureDOTS.Runtime.Agency
{
    /// <summary>
    /// Source category for control claims.
    /// </summary>
    public enum ControlClaimSourceKind : byte
    {
        None = 0,
        Operator = 1,
        Authority = 2,
        Hostile = 3,
        Scripted = 4
    }

    /// <summary>
    /// External control assertions applied to an entity.
    /// Processed by AgencyControlClaimBridgeSystem into ControlLink entries.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct ControlClaim : IBufferElementData
    {
        public Entity Controller;
        public Entity SourceSeat;
        public AgencyDomain Domains;
        public float Pressure;
        public float Legitimacy;
        public float Hostility;
        public float Consent;
        public uint EstablishedTick;
        public uint ExpireTick;
        public ControlClaimSourceKind SourceKind;
        public byte Reserved0;
        public ushort Reserved1;
    }
}
