using Unity.Entities;

namespace PureDOTS.Runtime.Ships
{
    public enum ShipRole : byte
    {
        Carrier,
        Cruiser,
        Battleship,
        Freighter,
        Civilian,
        Support
    }

    public struct ShipAggregate : IComponentData
    {
        public ShipRole Role;
        public int HousingCapacity;
        public int ServiceLevel;
        public int FacilitySlots;
    }

    public struct CrewAggregate : IComponentData
    {
        public Entity ParentShip;
        public float AverageMorale;
        public float AverageOutlook;
        public int MemberCount;
    }

    public struct CrewContract : IComponentData
    {
        public float DurationYears;
        public float TimeServed;
        public bool Mandatory;
    }
}
