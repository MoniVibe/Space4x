using Unity.Entities;

namespace PureDOTS.Runtime
{
    /// <summary>
    /// ECS component storing scenario configuration values for simulation/game bootstraps.
    /// Created by ScenarioConfigAuthoring from ScenarioDef asset.
    /// </summary>
    public struct ScenarioConfig : IComponentData
    {
        public bool EnableGodgame;
        public bool EnableSpace4x;
        public bool EnableEconomy;
        public uint GodgameSeed;
        public uint Space4xSeed;
        public int VillageCount;
        public int VillagersPerVillage;
        public int CarrierCount;
        public int AsteroidCount;
        public int StartingBandCount;
        public float Difficulty;
        public float Density;
    }

    /// <summary>
    /// Tag component indicating scenario spawning is complete.
    /// </summary>
    public struct ScenarioCompleteTag : IComponentData
    {
    }
}
