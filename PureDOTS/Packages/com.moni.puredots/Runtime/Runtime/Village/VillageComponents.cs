using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Village
{
    /// <summary>
    /// Tag component identifying a village entity.
    /// </summary>
    public struct VillageTag : IComponentData
    {
    }

    /// <summary>
    /// Unique identifier for a village.
    /// </summary>
    public struct VillageId : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// Alignment for a village (affects behavior, relations).
    /// </summary>
    public struct VillageAlignment : IComponentData
    {
        public float LawChaos;      // -1 = Chaotic, 0 = Neutral, 1 = Lawful
        public float GoodEvil;      // -1 = Evil, 0 = Neutral, 1 = Good
        public float OrderChaos;    // -1 = Chaos, 0 = Neutral, 1 = Order
    }

    /// <summary>
    /// Resource storage for a village.
    /// </summary>
    public struct VillageResources : IComponentData
    {
        public float Food;
        public float Wood;
        public float Stone;
        public float Ore;
        public float Metal;
        public float Fuel;
    }

    /// <summary>
    /// Tag component identifying a house building.
    /// </summary>
    public struct HouseTag : IComponentData { }

    /// <summary>
    /// Tag component identifying a storage building.
    /// </summary>
    public struct StorageTag : IComponentData { }

    /// <summary>
    /// Tag component identifying a lumberyard building.
    /// </summary>
    public struct LumberyardTag : IComponentData { }

    /// <summary>
    /// Tag component identifying a mine building.
    /// </summary>
    public struct MineTag : IComponentData { }

    /// <summary>
    /// Tag component identifying a field building.
    /// </summary>
    public struct FieldTag : IComponentData { }

    /// <summary>
    /// Tag component identifying a Smelter building.
    /// </summary>
    public struct SmelterTag : IComponentData { }
}

