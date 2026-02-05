using PureDOTS.Runtime.Modules;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public struct EngineClassTuning
    {
        public float ThrustMultiplier;
        public float TurnMultiplier;
        public float ResponseMultiplier;
        public float EfficiencyMultiplier;
        public float BoostMultiplier;
    }

    public struct EngineFuelTuning
    {
        public float ThrustMultiplier;
        public float ResponseBias;
        public float EfficiencyBias;
        public float BoostBias;
    }

    public struct EngineIntakeTuning
    {
        public float ThrustMultiplier;
        public float ResponseBias;
        public float EfficiencyBias;
        public float BoostBias;
    }

    public struct EngineVectoringTuning
    {
        public float TurnMultiplier;
        public float BaseVectoring;
    }

    public struct Space4XEngineTuningBlob
    {
        public BlobArray<EngineClassTuning> ClassTuning;
        public BlobArray<EngineFuelTuning> FuelTuning;
        public BlobArray<EngineIntakeTuning> IntakeTuning;
        public BlobArray<EngineVectoringTuning> VectoringTuning;
    }

    public struct Space4XEngineTuningSingleton : IComponentData
    {
        public BlobAssetReference<Space4XEngineTuningBlob> Blob;
    }

    public static class Space4XEngineTuningDefaults
    {
        public const int EngineClassCount = 4;
        public const int EngineFuelCount = 5;
        public const int EngineIntakeCount = 4;
        public const int EngineVectoringCount = 4;

        public static int ResolveClassIndex(EngineClass engineClass)
        {
            var index = (int)engineClass;
            return index >= 0 && index < EngineClassCount ? index : 0;
        }

        public static int ResolveFuelIndex(EngineFuelType fuelType)
        {
            var index = (int)fuelType;
            return index >= 0 && index < EngineFuelCount ? index : 0;
        }

        public static int ResolveIntakeIndex(EngineIntakeType intakeType)
        {
            var index = (int)intakeType;
            return index >= 0 && index < EngineIntakeCount ? index : 0;
        }

        public static int ResolveVectoringIndex(EngineVectoringMode vectoringMode)
        {
            var index = (int)vectoringMode;
            return index >= 0 && index < EngineVectoringCount ? index : 0;
        }

        public static EngineClassTuning DefaultClassTuning(EngineClass engineClass)
        {
            return engineClass switch
            {
                EngineClass.Military => new EngineClassTuning
                {
                    ThrustMultiplier = 1.1f,
                    TurnMultiplier = 1.1f,
                    ResponseMultiplier = 1.0f,
                    EfficiencyMultiplier = 0.95f,
                    BoostMultiplier = 1.1f
                },
                EngineClass.Capital => new EngineClassTuning
                {
                    ThrustMultiplier = 1.0f,
                    TurnMultiplier = 0.8f,
                    ResponseMultiplier = 0.8f,
                    EfficiencyMultiplier = 1.1f,
                    BoostMultiplier = 0.9f
                },
                EngineClass.Experimental => new EngineClassTuning
                {
                    ThrustMultiplier = 1.2f,
                    TurnMultiplier = 1.15f,
                    ResponseMultiplier = 1.15f,
                    EfficiencyMultiplier = 0.9f,
                    BoostMultiplier = 1.25f
                },
                EngineClass.Civilian => new EngineClassTuning
                {
                    ThrustMultiplier = 0.95f,
                    TurnMultiplier = 0.95f,
                    ResponseMultiplier = 0.9f,
                    EfficiencyMultiplier = 1.05f,
                    BoostMultiplier = 0.9f
                },
                _ => new EngineClassTuning
                {
                    ThrustMultiplier = 1f,
                    TurnMultiplier = 1f,
                    ResponseMultiplier = 1f,
                    EfficiencyMultiplier = 1f,
                    BoostMultiplier = 1f
                }
            };
        }

        public static EngineFuelTuning DefaultFuelTuning(EngineFuelType fuelType)
        {
            return fuelType switch
            {
                EngineFuelType.Chemical => new EngineFuelTuning
                {
                    ThrustMultiplier = 1.05f,
                    ResponseBias = 0.05f,
                    EfficiencyBias = -0.1f,
                    BoostBias = 0.12f
                },
                EngineFuelType.Ion => new EngineFuelTuning
                {
                    ThrustMultiplier = 0.85f,
                    ResponseBias = -0.05f,
                    EfficiencyBias = 0.2f,
                    BoostBias = -0.2f
                },
                EngineFuelType.Fusion => new EngineFuelTuning
                {
                    ThrustMultiplier = 1.0f,
                    ResponseBias = 0.04f,
                    EfficiencyBias = 0.08f,
                    BoostBias = 0.08f
                },
                EngineFuelType.Antimatter => new EngineFuelTuning
                {
                    ThrustMultiplier = 1.15f,
                    ResponseBias = 0.08f,
                    EfficiencyBias = -0.02f,
                    BoostBias = 0.2f
                },
                EngineFuelType.Exotic => new EngineFuelTuning
                {
                    ThrustMultiplier = 1.1f,
                    ResponseBias = 0.1f,
                    EfficiencyBias = 0.05f,
                    BoostBias = 0.15f
                },
                _ => new EngineFuelTuning
                {
                    ThrustMultiplier = 1f,
                    ResponseBias = 0f,
                    EfficiencyBias = 0f,
                    BoostBias = 0f
                }
            };
        }

        public static EngineIntakeTuning DefaultIntakeTuning(EngineIntakeType intakeType)
        {
            return intakeType switch
            {
                EngineIntakeType.Scoop => new EngineIntakeTuning
                {
                    ThrustMultiplier = 1f,
                    ResponseBias = -0.04f,
                    EfficiencyBias = 0.06f,
                    BoostBias = 0f
                },
                EngineIntakeType.Ramjet => new EngineIntakeTuning
                {
                    ThrustMultiplier = 1.05f,
                    ResponseBias = -0.06f,
                    EfficiencyBias = 0.08f,
                    BoostBias = 0.05f
                },
                EngineIntakeType.ReactorFeed => new EngineIntakeTuning
                {
                    ThrustMultiplier = 1f,
                    ResponseBias = 0.02f,
                    EfficiencyBias = 0.03f,
                    BoostBias = 0.02f
                },
                _ => new EngineIntakeTuning
                {
                    ThrustMultiplier = 1f,
                    ResponseBias = 0f,
                    EfficiencyBias = 0f,
                    BoostBias = 0f
                }
            };
        }

        public static EngineVectoringTuning DefaultVectoringTuning(EngineVectoringMode vectoringMode)
        {
            return vectoringMode switch
            {
                EngineVectoringMode.Fixed => new EngineVectoringTuning
                {
                    TurnMultiplier = 0.9f,
                    BaseVectoring = 0.2f
                },
                EngineVectoringMode.Gimbaled => new EngineVectoringTuning
                {
                    TurnMultiplier = 1.0f,
                    BaseVectoring = 0.5f
                },
                EngineVectoringMode.Vectored => new EngineVectoringTuning
                {
                    TurnMultiplier = 1.12f,
                    BaseVectoring = 0.75f
                },
                EngineVectoringMode.Omnidirectional => new EngineVectoringTuning
                {
                    TurnMultiplier = 1.25f,
                    BaseVectoring = 1f
                },
                _ => new EngineVectoringTuning
                {
                    TurnMultiplier = 1f,
                    BaseVectoring = 0.5f
                }
            };
        }

        public static EngineClassTuning Sanitize(EngineClassTuning tuning)
        {
            tuning.ThrustMultiplier = math.max(0f, tuning.ThrustMultiplier);
            tuning.TurnMultiplier = math.max(0f, tuning.TurnMultiplier);
            tuning.ResponseMultiplier = math.max(0f, tuning.ResponseMultiplier);
            tuning.EfficiencyMultiplier = math.max(0f, tuning.EfficiencyMultiplier);
            tuning.BoostMultiplier = math.max(0f, tuning.BoostMultiplier);
            return tuning;
        }

        public static EngineFuelTuning Sanitize(EngineFuelTuning tuning)
        {
            tuning.ThrustMultiplier = math.max(0f, tuning.ThrustMultiplier);
            return tuning;
        }

        public static EngineIntakeTuning Sanitize(EngineIntakeTuning tuning)
        {
            tuning.ThrustMultiplier = math.max(0f, tuning.ThrustMultiplier);
            return tuning;
        }

        public static EngineVectoringTuning Sanitize(EngineVectoringTuning tuning)
        {
            tuning.TurnMultiplier = math.max(0f, tuning.TurnMultiplier);
            tuning.BaseVectoring = math.saturate(tuning.BaseVectoring);
            return tuning;
        }
    }
}
