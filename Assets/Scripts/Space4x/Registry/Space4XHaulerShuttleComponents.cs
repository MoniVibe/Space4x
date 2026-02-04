using Unity.Entities;

namespace Space4X.Registry
{
    public enum Space4XHaulerShuttlePhase : byte
    {
        Idle = 0,
        ToCarrier = 1,
        ToColony = 2
    }

    /// <summary>
    /// Minimal state for hauler shuttle behavior between carriers and colonies.
    /// </summary>
    public struct Space4XHaulerShuttleState : IComponentData
    {
        public Entity TargetCarrier;
        public Entity TargetColony;
        public ResourceType CargoType;
        public float CargoAmount;
        public Space4XHaulerShuttlePhase Phase;
    }

    /// <summary>
    /// Tuning for hauler shuttle transfers.
    /// </summary>
    public struct Space4XHaulerShuttleConfig : IComponentData
    {
        public float PickupRadius;
        public float DropoffRadius;
        public float Speed;
        public float TransferRatePerSecond;
        public float MinCarrierLoad;

        public static Space4XHaulerShuttleConfig Default => new Space4XHaulerShuttleConfig
        {
            PickupRadius = 6f,
            DropoffRadius = 8f,
            Speed = 6f,
            TransferRatePerSecond = 300f,
            MinCarrierLoad = 200f
        };
    }
}
