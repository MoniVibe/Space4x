using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Armies
{
    public struct ArmyId : IComponentData
    {
        public int Value;
        public int FactionId;
    }

    public struct ArmyStats : IComponentData
    {
        public int MemberCount;
        public float Morale;
        public float Cohesion;
        public float SupplyLevel;
        public float Fatigue;
        public uint LastUpdateTick;
    }

    public struct ArmyOrder : IComponentData
    {
        public enum OrderType : byte
        {
            Idle = 0,
            Defend = 1,
            Raid = 2,
            Escort = 3,
            Patrol = 4
        }

        public OrderType Type;
        public float3 TargetPosition;
        public Entity TargetEntity;
        public uint IssuedTick;
    }

    public struct ArmyMember : IBufferElementData
    {
        public Entity Member;
        public float Loyalty;
        public float Readiness;
    }

    public struct ArmyIntent : IComponentData
    {
        public ArmyOrder.OrderType DesiredOrder;
        public float IntentWeight;
    }
}
