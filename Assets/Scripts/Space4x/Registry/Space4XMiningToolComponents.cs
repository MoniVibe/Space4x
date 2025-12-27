using PureDOTS.Environment;
using Unity.Entities;

namespace Space4X.Registry
{
    public struct Space4XMiningToolProfile : IComponentData
    {
        public TerrainModificationToolKind ToolKind;
        public byte HasShapeOverride;
        public TerrainModificationShape Shape;
        public float RadiusOverride;
        public float RadiusMultiplier;
        public float StepLengthOverride;
        public float StepLengthMultiplier;
        public float DigUnitsPerMeterOverride;
        public float MinStepLengthOverride;
        public float MaxStepLengthOverride;
        public float YieldMultiplier;
        public float HeatDeltaMultiplier;
        public float InstabilityDeltaMultiplier;
        public byte DamageDeltaOverride;
        public byte DamageThresholdOverride;

        public static Space4XMiningToolProfile Default => new()
        {
            ToolKind = TerrainModificationToolKind.Drill,
            HasShapeOverride = 0,
            Shape = TerrainModificationShape.Brush,
            RadiusOverride = 0f,
            RadiusMultiplier = 1f,
            StepLengthOverride = 0f,
            StepLengthMultiplier = 1f,
            DigUnitsPerMeterOverride = 0f,
            MinStepLengthOverride = 0f,
            MaxStepLengthOverride = 0f,
            YieldMultiplier = 1f,
            HeatDeltaMultiplier = 1f,
            InstabilityDeltaMultiplier = 1f,
            DamageDeltaOverride = 0,
            DamageThresholdOverride = 0
        };
    }
}
