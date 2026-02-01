using PureDOTS.Runtime.Agency;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum SeatRecommendationSource : byte
    {
        None = 0,
        Weapons = 1,
        Sensors = 2,
        Logistics = 3
    }

    [InternalBufferCapacity(2)]
    public struct SeatRecommendation : IBufferElementData
    {
        public SeatRecommendationSource Source;
        public AgencyDomain Domain;
        public CaptainOrderType OrderType;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public byte Priority;
        public half Confidence;
        public uint RecommendedTick;
    }
}
