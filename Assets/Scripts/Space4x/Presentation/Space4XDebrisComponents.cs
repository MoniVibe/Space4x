using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    public struct Space4XDebrisTag : IComponentData
    {
    }

    public struct Space4XDebrisMotion : IComponentData
    {
        public float3 Velocity;
        public float Lifetime;
        public float Drag;
    }

    public struct Space4XDebrisSpallConfig : IComponentData
    {
        public float PiecesPerVoxel;
        public int MaxPiecesPerEvent;
        public int MaxPiecesPerFrame;
        public float ImpulseMin;
        public float ImpulseMax;
        public float LifetimeMin;
        public float LifetimeMax;
        public float ScaleMin;
        public float ScaleMax;
        public float Drag;
        public float DrillImpulseMultiplier;
        public float LaserImpulseMultiplier;
        public float MicrowaveImpulseMultiplier;

        public static Space4XDebrisSpallConfig Default => new()
        {
            PiecesPerVoxel = 0.01f,
            MaxPiecesPerEvent = 18,
            MaxPiecesPerFrame = 48,
            ImpulseMin = 1.5f,
            ImpulseMax = 6f,
            LifetimeMin = 1.5f,
            LifetimeMax = 4.5f,
            ScaleMin = 0.15f,
            ScaleMax = 0.45f,
            Drag = 0.4f,
            DrillImpulseMultiplier = 0.9f,
            LaserImpulseMultiplier = 1.2f,
            MicrowaveImpulseMultiplier = 1.05f
        };
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public struct Space4XDebrisSpallFrameStats : IComponentData
    {
        public int DebrisSpawnedThisFrame;
        public int DebrisSpawnEventsThisFrame;
        public int DebrisSuppressedByBudget;
    }
#endif
}
