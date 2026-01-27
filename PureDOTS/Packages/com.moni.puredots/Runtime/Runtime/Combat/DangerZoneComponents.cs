using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    public struct FiringArc : IComponentData
    {
        public float3 Forward;
        public float AngleDegrees;
        public float Range;
        public float AntiCraftThreat;
        public float CapitalThreat;
    }

    public struct PilotAwareness : IComponentData
    {
        public float ArcSensitivity;
        public float ThreatSensitivity;
    }
}
