using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Agency
{
    /// <summary>
    /// Declares a hostile control override against an entity's agency domains.
    /// </summary>
    public struct HostileControlOverride : IComponentData
    {
        public Entity Controller;
        public AgencyDomain Domains;
        public float Pressure;
        public float Legitimacy;
        public float Hostility;
        public float Consent;
        public uint DurationTicks;
        public uint EstablishedTick;
        public uint ExpireTick;
        public byte Active;
        public byte LastReportedActive;
        public ushort Reserved0;
        public FixedString64Bytes Reason;
    }
}
