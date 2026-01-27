using Unity.Entities;

namespace PureDOTS.Runtime.Ships
{
    public enum ShipStatus : byte
    {
        ColdStorage,
        Crewed,
        CaptainMissing,
        Damaged
    }

    public struct ShipOwnership : IComponentData
    {
        public Entity OwnerEntity;
        public ShipStatus Status;
    }

    public struct CrewWageExpectation : IComponentData
    {
        public float WagePerYear;
        public float AlignmentPreference;
    }
}
