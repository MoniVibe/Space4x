using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Visuals
{
    public enum MiningVisualType : byte
    {
        Villager = 0,
        Vessel = 1
    }

    public struct MiningVisualManifest : IComponentData
    {
        public int VillagerNodeCount;
        public float VillagerThroughput;
        public int VesselCount;
        public float VesselThroughput;
        public uint LastSyncTick;
        public float VillagerDeliveredCumulative;
        public float VesselLoadCumulative;
    }

    public struct MiningVisualPrefab : IComponentData
    {
        public MiningVisualType VisualType;
        public float BaseScale;
        public Entity Prefab;
        public Entity FxPrefab;
    }

    public struct MiningVisual : IComponentData
    {
        public MiningVisualType VisualType;
        public Entity SourceEntity;
    }

    public struct MiningVisualRequest : IBufferElementData
    {
        public MiningVisualType VisualType;
        public Entity SourceEntity;
        public float3 Position;
        public float BaseScale;
    }
}

