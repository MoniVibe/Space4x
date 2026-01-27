using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    public enum MiningLoopPhase : byte
    {
        Idle = 0,
        TravellingToHarvest = 1,
        Harvesting = 2,
        TravellingToDropoff = 3,
        DroppingOff = 4
    }

    public struct MiningLoopConfig : IComponentData
    {
        public float MaxCargo;
        public float HarvestRatePerSecond;
        public float DropoffRatePerSecond;
        public float TravelSpeedMetersPerSecond;
        public byte CanSelfDeliver;
    }

    public struct MiningLoopState : IComponentData
    {
        public MiningLoopPhase Phase;
        public float PhaseTimer;
        public float CurrentCargo;
    }
}
