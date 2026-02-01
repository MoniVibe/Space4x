using Unity.Entities;

namespace Space4X.Registry
{
    public enum ShipAlertLevel : byte
    {
        Normal = 0,
        Caution = 1,
        Critical = 2
    }

    public struct ShipSystemsSnapshot : IComponentData
    {
        public float HullRatio;
        public float ShieldRatio;
        public float ArmorRating;
        public float FuelRatio;
        public float AmmoRatio;
        public float SensorRange;
        public float SensorAcuity;
        public float ThrustAuthority;
        public float TurnAuthority;
        public float BridgeTechLevel;
        public float NavigationCohesion;
        public byte WeaponMounts;
        public byte WeaponsOnline;
        public byte ContactsTracked;
        public byte Reserved;
        public uint UpdatedTick;
    }

    public struct SeatConsoleState : IComponentData
    {
        public float ConsoleQuality;
        public float DataLatencySeconds;
        public float DataFidelity;
        public uint UpdatedTick;
    }

    public struct SeatInstrumentFeed : IComponentData
    {
        public float HullRatio;
        public float ShieldRatio;
        public float ArmorRating;
        public float FuelRatio;
        public float AmmoRatio;
        public float SensorRange;
        public float SensorAcuity;
        public float ThrustAuthority;
        public float TurnAuthority;
        public float BridgeTechLevel;
        public float NavigationCohesion;
        public byte WeaponMounts;
        public byte WeaponsOnline;
        public byte ContactsTracked;
        public byte Reserved;
        public uint UpdatedTick;
    }

    public struct CaptainAggregateBrief : IComponentData
    {
        public ShipAlertLevel AlertLevel;
        public byte Reserved0;
        public ushort Reserved1;
        public float HullRatio;
        public float ShieldRatio;
        public float FuelRatio;
        public float AmmoRatio;
        public byte ContactsTracked;
        public byte WeaponsOnline;
        public ushort Reserved2;
        public uint UpdatedTick;
    }
}
