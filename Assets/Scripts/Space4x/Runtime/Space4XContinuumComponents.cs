using Unity.Entities;

namespace Space4X.Runtime
{
    public enum Space4XContinuumTier : byte
    {
        Unknown = 0,
        DeepOrbit = 1,
        OperationalOrbit = 2,
        NearOrbitalCombat = 3,
        ApproachShell = 4,
        SurfaceLocal = 5
    }

    public struct Space4XContinuumConfig : IComponentData
    {
        public byte Enabled;
        public float DefaultPlanetRadius;
        public float DeepOrbitMinRatio;
        public float OperationalOrbitMinRatio;
        public float NearOrbitalMinRatio;
        public float ApproachShellMinRatio;
        public float SurfaceLocalMinRatio;
        public float HysteresisRatio;

        public static Space4XContinuumConfig Default => new Space4XContinuumConfig
        {
            Enabled = 0,
            DefaultPlanetRadius = 120f,
            DeepOrbitMinRatio = 8f,
            OperationalOrbitMinRatio = 2f,
            NearOrbitalMinRatio = 1.05f,
            ApproachShellMinRatio = 1.005f,
            SurfaceLocalMinRatio = 0.999f,
            HysteresisRatio = 0.01f
        };
    }

    public struct Space4XContinuumState : IComponentData
    {
        public Entity AnchorPlanetFrame;
        public Space4XContinuumTier Tier;
        public float RadiusRatio;
        public float DistanceToPlanetCenter;
        public float PlanetRadius;
        public uint LastTransitionTick;
        public uint TransitionCount;
    }
}

