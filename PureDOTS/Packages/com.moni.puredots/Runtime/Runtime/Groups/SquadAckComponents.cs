using Unity.Entities;

namespace PureDOTS.Runtime.Groups
{
    /// <summary>
    /// Tracks last issued squad order tick acknowledged per entity to avoid duplicate visual acks.
    /// </summary>
    public struct SquadAckState : IComponentData
    {
        public uint LastAckTick;
    }
}





