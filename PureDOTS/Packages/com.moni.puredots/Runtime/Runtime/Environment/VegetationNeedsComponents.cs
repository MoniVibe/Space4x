using Unity.Entities;

namespace PureDOTS.Runtime.Environment
{
    /// <summary>
    /// Environmental needs for vegetation.
    /// Defines the optimal and acceptable ranges for sunlight, moisture, and temperature.
    /// </summary>
    public struct VegetationNeeds : IComponentData
    {
        /// <summary>Minimum required sunlight (0-1).</summary>
        public float SunlightMin;

        /// <summary>Maximum tolerated sunlight (0-1).</summary>
        public float SunlightMax;

        /// <summary>Minimum required moisture (0-1).</summary>
        public float MoistureMin;

        /// <summary>Maximum tolerated moisture (0-1).</summary>
        public float MoistureMax;

        /// <summary>Minimum required temperature.</summary>
        public float TempMin;

        /// <summary>Maximum tolerated temperature.</summary>
        public float TempMax;

        /// <summary>Root depth (affects drainage interaction if implemented).</summary>
        public float RootDepth;

        /// <summary>Moisture consumption rate (how much moisture this plant uses per tick).</summary>
        public float MoistureUsage;
    }

    /// <summary>
    /// Current stress and growth state for vegetation.
    /// Updated by VegetationStressSystem based on environment vs needs.
    /// </summary>
    public struct VegetationStress : IComponentData
    {
        /// <summary>Current stress level (0-1, where 0 = healthy, 1 = dying).</summary>
        public float Stress;

        /// <summary>Current growth factor (0-1, multiplier for growth rate).</summary>
        public float GrowthFactor;

        /// <summary>Last stress check tick.</summary>
        public uint LastStressCheckTick;

        /// <summary>Moisture factor (0-1, how well moisture needs are met).</summary>
        public float MoistureFactor;

        /// <summary>Temperature factor (0-1, how well temperature needs are met).</summary>
        public float TempFactor;

        /// <summary>Sunlight factor (0-1, how well sunlight needs are met).</summary>
        public float SunlightFactor;
    }

    /// <summary>
    /// Growth state for vegetation instances.
    /// Tracks growth stage and progress within current stage.
    /// </summary>
    public struct VegetationGrowthState : IComponentData
    {
        /// <summary>Current growth stage (0=Seed, 1=Sprout, 2=Young, 3=Adult, 4=Mature).</summary>
        public byte GrowthStage;

        /// <summary>Growth progress within current stage (0-1).</summary>
        public float GrowthProgress;

        /// <summary>Base growth rate (progress per tick under optimal conditions).</summary>
        public float BaseGrowthRate;

        /// <summary>Last growth update tick.</summary>
        public uint LastGrowthTick;
    }
}
























