using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public enum Space4XReferenceFrameKind : byte
    {
        Galaxy = 0,
        StarSystem = 1,
        Planet = 2,
        LocalBubble = 3,
        ShipBubble = 4
    }

    public struct Space4XReferenceFrame : IComponentData
    {
        public Space4XReferenceFrameKind Kind;
        public Entity ParentFrame;
        public byte IsOnRails;
        public byte Reserved0;
        public ushort Reserved1;
        public uint EpochTick;
        public double3 PositionInParent;
        public double3 VelocityInParent;
    }

    public struct Space4XFrameTransform : IComponentData
    {
        public double3 PositionWorld;
        public double3 VelocityWorld;
        public uint UpdatedTick;
    }

    public struct Space4XOrbitalElements : IComponentData
    {
        public double SemiMajorAxis;
        public double Eccentricity;
        public double Inclination;
        public double LongitudeOfAscendingNode;
        public double ArgumentOfPeriapsis;
        public double MeanAnomalyAtEpoch;
        public double Mu;
        public uint EpochTick;
    }

    public struct Space4XSOIRegion : IComponentData
    {
        public double EnterRadius;
        public double ExitRadius;
    }

    public struct Space4XFrameMembership : IComponentData
    {
        public Entity Frame;
        public float3 LocalPosition;
        public float3 LocalVelocity;
    }

    public struct Space4XFrameTransition : IComponentData
    {
        public Entity FromFrame;
        public Entity ToFrame;
        public double3 WorldPosition;
        public double3 WorldVelocity;
        public uint TransitionTick;
        public byte Pending;
        public byte Reserved0;
        public ushort Reserved1;
    }

    public struct Space4XReferenceFrameRootTag : IComponentData { }

    public struct Space4XReferenceFrameStarSystemTag : IComponentData { }
    public struct Space4XReferenceFramePlanetTag : IComponentData { }
    public struct Space4XReferenceFrameProbeTag : IComponentData { }
    public struct Space4XFrameMotionTag : IComponentData { }
    public struct Space4XFrameDrivenTransformTag : IComponentData { }

    public struct Space4XFrameTransitionMetrics : IComponentData
    {
        public uint Tick;
        public int ProcessedCount;
    }

    public struct Space4XReferenceFrameConfig : IComponentData
    {
        public byte Enabled;
        public byte Reserved0;
        public ushort Reserved1;
        public float LocalBubbleRadius;
        public float EnterSOIMultiplier;
        public float ExitSOIMultiplier;
    }
}
