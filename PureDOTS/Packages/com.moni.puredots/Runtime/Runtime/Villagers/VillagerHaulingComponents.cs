using Unity.Entities;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Tracks villager-specific opportunistic hauling cooldowns / telemetry.
    /// </summary>
    public struct VillagerHaulingState : IComponentData
    {
        public uint CooldownUntilTick;
        public Entity LastSiteEntity;
        public ushort LastResourceTypeIndex;
        public float LastUnits;
        public uint LastHaulTick;
    }
}





