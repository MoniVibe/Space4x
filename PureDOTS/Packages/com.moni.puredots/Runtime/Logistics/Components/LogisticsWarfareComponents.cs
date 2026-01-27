using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics.Components
{
    /// <summary>
    /// Supply route summary for AI evaluation.
    /// Exposed per active job/route for raiders and escorts.
    /// </summary>
    public struct SupplyRouteSummary : IComponentData
    {
        public float Value; // Total cargo value
        public float Risk; // Route risk (0..1)
        public float Distance; // Route distance
        public float EscortStrength; // Current escort strength (0..1)
        public Entity RouteEntity; // Entity representing this route
    }

    /// <summary>
    /// Intercept intent component.
    /// Used by raiders to plan interception of haulers.
    /// </summary>
    public struct InterceptIntent : IComponentData
    {
        public Entity TargetHauler;
        public uint EarliestInterceptTick;
        public float3 PredictedInterceptPosition;
    }
}

