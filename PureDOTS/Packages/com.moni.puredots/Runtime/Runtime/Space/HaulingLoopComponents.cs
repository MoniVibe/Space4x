using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    public enum HaulingLoopPhase : byte
    {
        Idle = 0,
        TravellingToPickup = 1,
        Loading = 2,
        TravellingToDropoff = 3,
        Unloading = 4
    }

    public struct HaulingLoopConfig : IComponentData
    {
        public float MaxCargo;
        public float TravelSpeedMetersPerSecond;
        public float LoadRatePerSecond;
        public float UnloadRatePerSecond;
    }

    public struct HaulingLoopState : IComponentData
    {
        public HaulingLoopPhase Phase;
        public float PhaseTimer;
        public float CurrentCargo;
    }

    /// <summary>
    /// Marker component added to miners that drop resources locally for haulers.
    /// </summary>
    public struct DropOnlyHarvesterTag : IComponentData
    {
    }
}
