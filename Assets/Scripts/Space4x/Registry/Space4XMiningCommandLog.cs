using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum MiningCommandType : byte
    {
        Gather = 0,
        Spawn = 1,
        Pickup = 2
    }

    public struct MiningCommandLogEntry : IBufferElementData
    {
        public uint Tick;
        public MiningCommandType CommandType;
        public Entity SourceEntity;
        public Entity TargetEntity;
        public ResourceType ResourceType;
        public float Amount;
        public float3 Position;
    }
}
