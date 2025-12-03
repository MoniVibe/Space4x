using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Target selection strategy for mining vessels.
    /// Determines how miners choose which asteroid to mine.
    /// </summary>
    public struct MinerTargetStrategy : IComponentData
    {
        public enum Strategy : byte
        {
            Nearest = 0,      // Choose closest asteroid (fastest travel time)
            BestYield = 1,    // Choose asteroid with highest ResourceAmount / MiningRate
            Balanced = 2      // Weight both distance and yield: score = (yield) / (distance + 1)
        }

        public Strategy SelectionStrategy;
    }
}

