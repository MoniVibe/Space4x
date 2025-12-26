using Unity.Entities;

namespace Space4X.Registry
{
    public struct Space4XAsteroidVolumeConfig : IComponentData
    {
        public float Radius;
        public float CoreRadiusRatio;
        public float MantleRadiusRatio;
        public byte CrustMaterialId;
        public byte MantleMaterialId;
        public byte CoreMaterialId;
        public byte CoreDepositId;
        public byte CoreOreGrade;
        public float OreGradeExponent;
        public uint Seed;

        public static Space4XAsteroidVolumeConfig Default => new()
        {
            Radius = 20f,
            CoreRadiusRatio = 0.3f,
            MantleRadiusRatio = 0.7f,
            CrustMaterialId = 1,
            MantleMaterialId = 2,
            CoreMaterialId = 3,
            CoreDepositId = 1,
            CoreOreGrade = 200,
            OreGradeExponent = 2f,
            Seed = 1u
        };
    }

    public struct Space4XAsteroidVolumeState : IComponentData
    {
        public byte Initialized;
    }

    public struct Space4XMiningDigConfig : IComponentData
    {
        public float DrillRadius;
        public float MinStepLength;
        public float MaxStepLength;
        public float DigUnitsPerMeter;
        public float LaserRadius;
        public float LaserStepLength;
        public float LaserYieldMultiplier;
        public float LaserHeatDelta;
        public float LaserInstabilityDelta;
        public float MicrowaveRadius;
        public byte MicrowaveDamageDelta;
        public byte MicrowaveDamageThreshold;
        public float MicrowaveYieldMultiplier;
        public float MicrowaveHeatDelta;
        public float MicrowaveInstabilityDelta;
        public float CrustYieldMultiplier;
        public float MantleYieldMultiplier;
        public float CoreYieldMultiplier;
        public float OreGradeWeight;

        public static Space4XMiningDigConfig Default => new()
        {
            DrillRadius = 1.25f,
            MinStepLength = 0.1f,
            MaxStepLength = 1.25f,
            DigUnitsPerMeter = 20f,
            LaserRadius = 1.8f,
            LaserStepLength = 3f,
            LaserYieldMultiplier = 0.6f,
            LaserHeatDelta = 1.5f,
            LaserInstabilityDelta = 0.8f,
            MicrowaveRadius = 2.5f,
            MicrowaveDamageDelta = 12,
            MicrowaveDamageThreshold = 200,
            MicrowaveYieldMultiplier = 0.4f,
            MicrowaveHeatDelta = 2.5f,
            MicrowaveInstabilityDelta = 1.2f,
            CrustYieldMultiplier = 0.8f,
            MantleYieldMultiplier = 1.1f,
            CoreYieldMultiplier = 1.6f,
            OreGradeWeight = 0.5f
        };
    }
}
