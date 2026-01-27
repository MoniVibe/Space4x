using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Tech
{
    public struct TechLevel : IComponentData
    {
        public float Value;
        public uint LastUpdateTick;
    }

    public struct TechDiffusionSource : IComponentData
    {
        public float SpreadMultiplier;
        public float MaxRange;
        public int WaypointId;
    }

    public struct TechDiffusionState : IComponentData
    {
        public Entity LastSource;
        public float IncomingLevel;
        public float Progress;
        public float Distance;
        public float AppliedRate;
        public uint LastUpdateTick;
    }

    public struct TechDiffusionSettings : IComponentData
    {
        public float BaseRatePerTick;
        public float DistanceFalloff;
        public float SourceLevelFactor;
        public float MinProgressPerTick;

        public static TechDiffusionSettings CreateDefault()
        {
            return new TechDiffusionSettings
            {
                BaseRatePerTick = 0.05f,
                DistanceFalloff = 0.05f,
                SourceLevelFactor = 0.1f,
                MinProgressPerTick = 0.001f
            };
        }
    }
}
