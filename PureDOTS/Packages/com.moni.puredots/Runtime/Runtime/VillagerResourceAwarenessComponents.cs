using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Desired resource type for awareness filtering. Defaults to ushort.MaxValue for "any".
    /// </summary>
    public struct VillagerResourceNeed : IComponentData
    {
        public ushort ResourceTypeIndex;
    }

    /// <summary>
    /// Cached awareness of nearby resources and storehouses for a villager.
    /// </summary>
    public struct VillagerResourceAwareness : IComponentData
    {
        public Entity KnownNode;
        public ushort ResourceTypeIndex;
        public float Confidence;
        public uint LastSeenTick;
        public Entity KnownStorehouse;
    }

    /// <summary>
    /// Tuning values for how villagers refresh resource awareness.
    /// </summary>
    public struct VillagerResourceAwarenessConfig : IComponentData
    {
        public float MaxDistance;
        public float MinConfidence;
        public int CadenceTicks;
        public uint StaleTicks;

        public static VillagerResourceAwarenessConfig Default => new VillagerResourceAwarenessConfig
        {
            MaxDistance = 30f,
            MinConfidence = 0.15f,
            CadenceTicks = 10,
            StaleTicks = 600u
        };
    }
}
