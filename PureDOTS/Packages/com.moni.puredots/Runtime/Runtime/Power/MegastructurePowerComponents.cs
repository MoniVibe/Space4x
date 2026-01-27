using Unity.Entities;

namespace PureDOTS.Runtime.Power
{
    /// <summary>
    /// Component marking an orbital power relay that bridges different power domains.
    /// </summary>
    public struct OrbitalPowerRelay : IComponentData
    {
        public PowerDomain SourceDomain;      // SystemWide, Orbital
        public PowerDomain TargetDomain;      // Orbital, GroundLocal, ShipLocal
        public float RelayEfficiency;         // 0..1, transmission efficiency
        public float MaxRelayCapacity;        // MW
    }
}

