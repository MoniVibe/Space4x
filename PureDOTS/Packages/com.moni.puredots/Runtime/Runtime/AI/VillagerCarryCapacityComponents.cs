using Unity.Entities;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Optional carry capacity modifier for villagers (gear, wagons, traits).
    /// </summary>
    public struct VillagerCarryCapacity : IComponentData
    {
        public float Multiplier;
        public float Bonus;

        public static VillagerCarryCapacity Default => new VillagerCarryCapacity
        {
            Multiplier = 1f,
            Bonus = 0f
        };
    }
}
