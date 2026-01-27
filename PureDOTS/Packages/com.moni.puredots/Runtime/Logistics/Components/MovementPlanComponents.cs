using System;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics.Components
{
    /// <summary>
    /// Logistics features flags.
    /// Tech-gated capabilities for logistics systems.
    /// </summary>
    [Flags]
    public enum LogisticsFeatures : uint
    {
        StaticRoutes = 1 << 0,
        MovingTargetRendez = 1 << 1,
        MidRouteRedirect = 1 << 2,
        MultiHopOptimization = 1 << 3
    }

    /// <summary>
    /// Logistics tech profile component.
    /// Defines which logistics features are available (tech-gated).
    /// </summary>
    public struct LogisticsTechProfile : IComponentData
    {
        public LogisticsFeatures Features;
    }

    /// <summary>
    /// Movement plan component.
    /// Attached to bands/armies/fleets to describe their movement intent.
    /// Used by logistics systems to predict rendezvous points.
    /// </summary>
    public struct MovementPlan : IComponentData
    {
        public float3 CurrentTarget; // next waypoint
        public float Speed; // abstracted speed
        public float3 Velocity; // optional velocity vector
    }
}

