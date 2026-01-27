using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Formations
{
    public enum FormationType : byte
    {
        Line = 0,
        Wedge = 1,
        Sphere = 2,
        Column = 3
    }

    public struct FormationConfig : IComponentData
    {
        public FormationType Type;
        public float SlotSpacing;
        public int SlotCount;
    }

    public struct FormationSlot : IBufferElementData
    {
        public float3 LocalOffset;
        public Entity AssignedEntity;
    }

    public struct FormationLeader : IComponentData
    {
        public Entity LeaderEntity;
    }
}
