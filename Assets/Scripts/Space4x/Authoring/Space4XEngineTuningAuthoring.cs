using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Modules;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Engine Tuning")]
    public sealed class Space4XEngineTuningAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct EngineClassTuningEntry
        {
            public EngineClass EngineClass;
            public float ThrustMultiplier;
            public float TurnMultiplier;
            public float ResponseMultiplier;
            public float EfficiencyMultiplier;
            public float BoostMultiplier;
        }

        [Serializable]
        public struct EngineFuelTuningEntry
        {
            public EngineFuelType FuelType;
            public float ThrustMultiplier;
            public float ResponseBias;
            public float EfficiencyBias;
            public float BoostBias;
        }

        [Serializable]
        public struct EngineIntakeTuningEntry
        {
            public EngineIntakeType IntakeType;
            public float ThrustMultiplier;
            public float ResponseBias;
            public float EfficiencyBias;
            public float BoostBias;
        }

        [Serializable]
        public struct EngineVectoringTuningEntry
        {
            public EngineVectoringMode VectoringMode;
            public float TurnMultiplier;
            [Range(0f, 1f)] public float BaseVectoring;
        }

        [Header("Class Tuning Overrides")]
        public List<EngineClassTuningEntry> classTuning = new List<EngineClassTuningEntry>();

        [Header("Fuel Tuning Overrides")]
        public List<EngineFuelTuningEntry> fuelTuning = new List<EngineFuelTuningEntry>();

        [Header("Intake Tuning Overrides")]
        public List<EngineIntakeTuningEntry> intakeTuning = new List<EngineIntakeTuningEntry>();

        [Header("Vectoring Tuning Overrides")]
        public List<EngineVectoringTuningEntry> vectoringTuning = new List<EngineVectoringTuningEntry>();

        public sealed class Baker : Unity.Entities.Baker<Space4XEngineTuningAuthoring>
        {
            public override void Bake(Space4XEngineTuningAuthoring authoring)
            {
                if (authoring == null)
                {
                    UnityDebug.LogWarning("Space4XEngineTuningAuthoring is null.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<Space4XEngineTuningBlob>();

                var classArray = builder.Allocate(ref root.ClassTuning, Space4XEngineTuningDefaults.EngineClassCount);
                for (var i = 0; i < classArray.Length; i++)
                {
                    classArray[i] = Space4XEngineTuningDefaults.DefaultClassTuning((EngineClass)i);
                }

                var fuelArray = builder.Allocate(ref root.FuelTuning, Space4XEngineTuningDefaults.EngineFuelCount);
                for (var i = 0; i < fuelArray.Length; i++)
                {
                    fuelArray[i] = Space4XEngineTuningDefaults.DefaultFuelTuning((EngineFuelType)i);
                }

                var intakeArray = builder.Allocate(ref root.IntakeTuning, Space4XEngineTuningDefaults.EngineIntakeCount);
                for (var i = 0; i < intakeArray.Length; i++)
                {
                    intakeArray[i] = Space4XEngineTuningDefaults.DefaultIntakeTuning((EngineIntakeType)i);
                }

                var vectoringArray = builder.Allocate(ref root.VectoringTuning, Space4XEngineTuningDefaults.EngineVectoringCount);
                for (var i = 0; i < vectoringArray.Length; i++)
                {
                    vectoringArray[i] = Space4XEngineTuningDefaults.DefaultVectoringTuning((EngineVectoringMode)i);
                }

                ApplyClassOverrides(authoring, classArray);
                ApplyFuelOverrides(authoring, fuelArray);
                ApplyIntakeOverrides(authoring, intakeArray);
                ApplyVectoringOverrides(authoring, vectoringArray);

                var blobAsset = builder.CreateBlobAssetReference<Space4XEngineTuningBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Space4XEngineTuningSingleton { Blob = blobAsset });
            }

            private static void ApplyClassOverrides(Space4XEngineTuningAuthoring authoring, BlobBuilderArray<EngineClassTuning> array)
            {
                if (authoring.classTuning == null)
                {
                    return;
                }

                foreach (var entry in authoring.classTuning)
                {
                    var index = Space4XEngineTuningDefaults.ResolveClassIndex(entry.EngineClass);
                    if ((uint)index >= (uint)array.Length)
                    {
                        continue;
                    }

                    var tuning = new EngineClassTuning
                    {
                        ThrustMultiplier = entry.ThrustMultiplier,
                        TurnMultiplier = entry.TurnMultiplier,
                        ResponseMultiplier = entry.ResponseMultiplier,
                        EfficiencyMultiplier = entry.EfficiencyMultiplier,
                        BoostMultiplier = entry.BoostMultiplier
                    };
                    array[index] = Space4XEngineTuningDefaults.Sanitize(tuning);
                }
            }

            private static void ApplyFuelOverrides(Space4XEngineTuningAuthoring authoring, BlobBuilderArray<EngineFuelTuning> array)
            {
                if (authoring.fuelTuning == null)
                {
                    return;
                }

                foreach (var entry in authoring.fuelTuning)
                {
                    var index = Space4XEngineTuningDefaults.ResolveFuelIndex(entry.FuelType);
                    if ((uint)index >= (uint)array.Length)
                    {
                        continue;
                    }

                    var tuning = new EngineFuelTuning
                    {
                        ThrustMultiplier = entry.ThrustMultiplier,
                        ResponseBias = entry.ResponseBias,
                        EfficiencyBias = entry.EfficiencyBias,
                        BoostBias = entry.BoostBias
                    };
                    array[index] = Space4XEngineTuningDefaults.Sanitize(tuning);
                }
            }

            private static void ApplyIntakeOverrides(Space4XEngineTuningAuthoring authoring, BlobBuilderArray<EngineIntakeTuning> array)
            {
                if (authoring.intakeTuning == null)
                {
                    return;
                }

                foreach (var entry in authoring.intakeTuning)
                {
                    var index = Space4XEngineTuningDefaults.ResolveIntakeIndex(entry.IntakeType);
                    if ((uint)index >= (uint)array.Length)
                    {
                        continue;
                    }

                    var tuning = new EngineIntakeTuning
                    {
                        ThrustMultiplier = entry.ThrustMultiplier,
                        ResponseBias = entry.ResponseBias,
                        EfficiencyBias = entry.EfficiencyBias,
                        BoostBias = entry.BoostBias
                    };
                    array[index] = Space4XEngineTuningDefaults.Sanitize(tuning);
                }
            }

            private static void ApplyVectoringOverrides(Space4XEngineTuningAuthoring authoring, BlobBuilderArray<EngineVectoringTuning> array)
            {
                if (authoring.vectoringTuning == null)
                {
                    return;
                }

                foreach (var entry in authoring.vectoringTuning)
                {
                    var index = Space4XEngineTuningDefaults.ResolveVectoringIndex(entry.VectoringMode);
                    if ((uint)index >= (uint)array.Length)
                    {
                        continue;
                    }

                    var tuning = new EngineVectoringTuning
                    {
                        TurnMultiplier = entry.TurnMultiplier,
                        BaseVectoring = math.saturate(entry.BaseVectoring)
                    };
                    array[index] = Space4XEngineTuningDefaults.Sanitize(tuning);
                }
            }
        }
    }
}
