using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Broadcasts the latest fleet kinematics for interception queries.
    /// </summary>
    public struct FleetMovementBroadcast : IComponentData
    {
        public float3 Position;
        public float3 Velocity;
        public uint LastUpdateTick;
        public byte AllowsInterception;
        public byte TechTier;
    }

    /// <summary>
    /// Optional explicit velocity source for fleets.
    /// </summary>
    public struct FleetKinematics : IComponentData
    {
        public float3 Velocity;
    }

    /// <summary>
    /// Desired intercept or rendezvous course for a hauler or support vessel.
    /// </summary>
    public struct InterceptCourse : IComponentData
    {
        public Entity TargetFleet;
        public float3 InterceptPoint;
        public uint EstimatedInterceptTick;
        public byte UsesInterception;
    }

    /// <summary>
    /// Tech/configuration gate for interception behavior.
    /// </summary>
    public struct InterceptCapability : IComponentData
    {
        public float MaxSpeed;
        public byte TechTier;
        public byte AllowIntercept;
    }

    /// <summary>
    /// Request element for deterministic intercept routing through the registry queue.
    /// </summary>
    public struct InterceptRequest : IBufferElementData
    {
        public Entity Requester;
        public Entity Target;
        public byte Priority;
        public uint RequestTick;
        public byte RequireRendezvous;
    }

    /// <summary>
    /// Singleton marker for the intercept queue buffers.
    /// </summary>
    public struct Space4XFleetInterceptQueue : IComponentData
    {
    }

    public enum InterceptMode : byte
    {
        Rendezvous = 0,
        Intercept = 1
    }

    /// <summary>
    /// Command log entry so time/rewind agents can replay interception attempts.
    /// </summary>
    public struct FleetInterceptCommandLogEntry : IBufferElementData
    {
        public uint Tick;
        public Entity Requester;
        public Entity Target;
        public float3 InterceptPoint;
        public uint EstimatedInterceptTick;
        public InterceptMode Mode;
    }

    /// <summary>
    /// Aggregated counters for telemetry publishing.
    /// </summary>
    public struct Space4XFleetInterceptTelemetry : IComponentData
    {
        public uint LastAttemptTick;
        public uint InterceptAttempts;
        public uint RendezvousAttempts;
    }
}
