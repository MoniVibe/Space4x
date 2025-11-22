using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// Inspector-friendly wrapper for doctrine expectations that bakes into alignment components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XDoctrineAuthoring : MonoBehaviour
    {
        [SerializeField] private AlignmentBounds alignmentWindow = AlignmentBounds.Default();
        [SerializeField, Range(0f, 2f)] private float axisTolerance = 0.1f;
        [SerializeField, Range(0f, 2f)] private float outlookTolerance = 0.1f;
        [SerializeField, Range(0f, 1f)] private float chaosMutinyThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] private float lawfulContractFloor = 0.25f;
        [SerializeField, Range(0f, 2f)] private float suspicionGain = 0.15f;
        [SerializeField] private AxisExpectation[] axisExpectations = Array.Empty<AxisExpectation>();
        [SerializeField] private OutlookExpectation[] outlookExpectations = Array.Empty<OutlookExpectation>();

        private void OnValidate()
        {
            alignmentWindow = alignmentWindow.Clamp();
            axisTolerance = math.clamp(axisTolerance, 0f, 2f);
            outlookTolerance = math.clamp(outlookTolerance, 0f, 2f);
            chaosMutinyThreshold = math.clamp(chaosMutinyThreshold, 0f, 1f);
            lawfulContractFloor = math.clamp(lawfulContractFloor, 0f, 1f);
            suspicionGain = math.max(0f, suspicionGain);

            if (axisExpectations != null)
            {
                for (int i = 0; i < axisExpectations.Length; i++)
                {
                    axisExpectations[i] = axisExpectations[i].Clamp();
                }
            }

            if (outlookExpectations != null)
            {
                for (int i = 0; i < outlookExpectations.Length; i++)
                {
                    outlookExpectations[i] = outlookExpectations[i].Clamp();
                }
            }
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XDoctrineAuthoring>
        {
            public override void Bake(Space4XDoctrineAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                var profile = new DoctrineProfile
                {
                    AlignmentWindow = authoring.alignmentWindow.Clamp().ToWindow(),
                    AxisTolerance = (half)math.clamp(authoring.axisTolerance, 0f, 2f),
                    OutlookTolerance = (half)math.clamp(authoring.outlookTolerance, 0f, 2f),
                    ChaosMutinyThreshold = (half)math.clamp(authoring.chaosMutinyThreshold, 0f, 1f),
                    LawfulContractFloor = (half)math.clamp(authoring.lawfulContractFloor, 0f, 1f),
                    SuspicionGain = (half)math.max(0f, authoring.suspicionGain)
                };
                AddComponent(entity, profile);

                var axisBuffer = AddBuffer<DoctrineAxisExpectation>(entity);
                var fanaticCount = 0;

                if (authoring.axisExpectations != null)
                {
                    for (int i = 0; i < authoring.axisExpectations.Length; i++)
                    {
                        var expectation = authoring.axisExpectations[i].Clamp();
                        var min = expectation.Min;
                        var max = expectation.Max;
                        var span = math.max(math.abs(min), math.abs(max));
                        if (span >= 1.5f)
                        {
                            fanaticCount++;
                            if (fanaticCount > 2)
                            {
                                min = math.clamp(min, -1.5f, 1.5f);
                                max = math.clamp(max, -1.5f, 1.5f);
                            }
                        }

                        axisBuffer.Add(new DoctrineAxisExpectation
                        {
                            Axis = expectation.Axis,
                            Min = (half)min,
                            Max = (half)max
                        });
                    }
                }

                if (fanaticCount > 2)
                {
                    Debug.LogWarning($"[Space4XDoctrineAuthoring] {fanaticCount} fanatic ethic expectations defined; additional entries were clamped to ±1.5.", authoring);
                }

                var outlookBuffer = AddBuffer<DoctrineOutlookExpectation>(entity);
                if (authoring.outlookExpectations != null)
                {
                    for (int i = 0; i < authoring.outlookExpectations.Length; i++)
                    {
                        var expectation = authoring.outlookExpectations[i].Clamp();
                        outlookBuffer.Add(new DoctrineOutlookExpectation
                        {
                            OutlookId = expectation.Outlook,
                            MinimumWeight = (half)expectation.MinimumWeight
                        });
                    }
                }
            }
        }

        [Serializable]
        private struct AxisExpectation
        {
            public EthicAxisId Axis;
            [Range(-2f, 2f)] public float Min;
            [Range(-2f, 2f)] public float Max;

            public AxisExpectation Clamp()
            {
                var clampedMin = math.clamp(Min, -2f, 2f);
                var clampedMax = math.clamp(Max, -2f, 2f);
                if (clampedMax < clampedMin)
                {
                    (clampedMin, clampedMax) = (clampedMax, clampedMin);
                }

                return new AxisExpectation
                {
                    Axis = Axis,
                    Min = clampedMin,
                    Max = clampedMax
                };
            }
        }

        [Serializable]
        private struct OutlookExpectation
        {
            public OutlookId Outlook;
            [Range(-1f, 1f)] public float MinimumWeight;

            public OutlookExpectation Clamp()
            {
                var clampedWeight = math.clamp(MinimumWeight, -1f, 1f);
                return new OutlookExpectation
                {
                    Outlook = Outlook,
                    MinimumWeight = clampedWeight
                };
            }
        }

        [Serializable]
        private struct AlignmentBounds
        {
            [Range(-1f, 1f)] public float LawMin;
            [Range(-1f, 1f)] public float LawMax;
            [Range(-1f, 1f)] public float GoodMin;
            [Range(-1f, 1f)] public float GoodMax;
            [Range(-1f, 1f)] public float IntegrityMin;
            [Range(-1f, 1f)] public float IntegrityMax;

            public static AlignmentBounds Default()
            {
                return new AlignmentBounds
                {
                    LawMin = -1f,
                    LawMax = 1f,
                    GoodMin = -1f,
                    GoodMax = 1f,
                    IntegrityMin = -1f,
                    IntegrityMax = 1f
                };
            }

            public AlignmentBounds Clamp()
            {
                var lawMin = math.clamp(LawMin, -1f, 1f);
                var lawMax = math.clamp(LawMax, -1f, 1f);
                var goodMin = math.clamp(GoodMin, -1f, 1f);
                var goodMax = math.clamp(GoodMax, -1f, 1f);
                var integrityMin = math.clamp(IntegrityMin, -1f, 1f);
                var integrityMax = math.clamp(IntegrityMax, -1f, 1f);

                if (lawMax < lawMin) (lawMin, lawMax) = (lawMax, lawMin);
                if (goodMax < goodMin) (goodMin, goodMax) = (goodMax, goodMin);
                if (integrityMax < integrityMin) (integrityMin, integrityMax) = (integrityMax, integrityMin);

                return new AlignmentBounds
                {
                    LawMin = lawMin,
                    LawMax = lawMax,
                    GoodMin = goodMin,
                    GoodMax = goodMax,
                    IntegrityMin = integrityMin,
                    IntegrityMax = integrityMax
                };
            }

            public AlignmentWindow ToWindow()
            {
                var clamped = Clamp();
                return new AlignmentWindow
                {
                    LawMin = (half)clamped.LawMin,
                    LawMax = (half)clamped.LawMax,
                    GoodMin = (half)clamped.GoodMin,
                    GoodMax = (half)clamped.GoodMax,
                    IntegrityMin = (half)clamped.IntegrityMin,
                    IntegrityMax = (half)clamped.IntegrityMax
                };
            }
        }
    }
}
