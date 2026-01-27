using Unity.Entities;

namespace PureDOTS.Runtime.Ships
{
    /// <summary>
    /// Intel mask - tracks known modules and position accuracy.
    /// </summary>
    public struct IntelMask : IComponentData
    {
        public uint KnownModulesMask; // Bitmask of known module indices (bits 0-31)
        public float ModulePosNoise; // Position noise for unknown modules (meters)
    }

    /// <summary>
    /// Gunnery skill - affects targeting accuracy and critical hit chance.
    /// </summary>
    public struct GunnerySkill : IComponentData
    {
        public float CritBonus; // Bonus to critical hit chance (0-1)
        public float AimJitterReduction; // Reduction in aim jitter (0-1)
    }

    /// <summary>
    /// Salvageable - marks derelict ships as salvageable.
    /// </summary>
    public struct Salvageable : IComponentData
    {
        public byte Grade; // 0 = none, 1 = scrap, 2 = refit, 3 = claimable
    }

    /// <summary>
    /// Claim intent - marks intent to claim a derelict ship.
    /// </summary>
    public struct ClaimIntent : IComponentData
    {
        public Entity Claimer; // Entity attempting to claim
        public uint Tick; // Tick when claim intent was made
    }

    /// <summary>
    /// Crew pod - escape pod entity component.
    /// </summary>
    public struct CrewPod : IComponentData
    {
        public Entity SourceShip; // Ship that ejected this pod
        public byte Occupants; // Number of crew in pod
        public byte MaxSeats; // Maximum capacity
        public byte Rescued; // 0/1 flag
        public byte Captured; // 0/1 flag
    }
}

