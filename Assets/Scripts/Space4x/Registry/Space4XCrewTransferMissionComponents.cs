using Unity.Entities;

namespace Space4X.Registry
{
    public enum CrewTransferMissionStatus : byte
    {
        Pending = 0,
        EnRoute = 1,
        Arrived = 2,
        Completed = 3,
        Failed = 4
    }

    /// <summary>
    /// Mobile crew transfer mission assigned to a vessel.
    /// </summary>
    public struct CrewTransferMission : IComponentData
    {
        public Entity Target;
        public int ReservedCrew;
        public float ReservedTraining;
        public float TransferRadius;
        public uint RequestedTick;
        public uint LastUpdateTick;
        public CrewTransferMissionStatus Status;
        public byte AddedInterceptCapability;
    }
}
