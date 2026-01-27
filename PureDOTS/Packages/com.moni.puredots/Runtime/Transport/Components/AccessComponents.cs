using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Components
{
    /// <summary>
    /// Hyperway access level.
    /// </summary>
    public enum HyperwayAccessLevel : byte
    {
        None = 0,
        PassengerOnly = 1,
        Cargo = 2,
        MilitaryPriority = 3,
        FullAccess = 4
    }

    /// <summary>
    /// Hyperway access contract buffer element.
    /// Defines access permissions for a faction.
    /// </summary>
    public struct HyperwayAccessContract : IBufferElementData
    {
        public int FactionId;
        public HyperwayAccessLevel AccessLevel;
        public float DiscountFactor; // 0..1 discount on ticket prices
    }

    /// <summary>
    /// Hyperway security state component.
    /// Tracks security and sabotage risk for a node.
    /// </summary>
    public struct HyperwaySecurityState : IComponentData
    {
        public float PatrolStrength; // 0..1
        public float SabotageRisk; // 0..1
    }
}

