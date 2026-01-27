using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    public struct TargetingComputer : IComponentData
    {
        public float TrackingQuality;
        public float PredictionQuality;
    }

    public struct GunnerySkill : IComponentData
    {
        public float SkillLevel;
    }

    public struct ProjectileFlightSpec : IComponentData
    {
        public float Speed;
        public float BehaviorFactor;
    }

    public struct TargetingSolution : IComponentData
    {
        public float3 AimPosition;
        public float TimeToImpact;
        public Entity Target;
    }
}
