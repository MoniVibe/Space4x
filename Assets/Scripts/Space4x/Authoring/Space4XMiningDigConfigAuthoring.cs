using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Mining Dig Config")]
    public sealed class Space4XMiningDigConfigAuthoring : MonoBehaviour
    {
        [Header("Drill")]
        [SerializeField, Range(0.1f, 10f)] private float drillRadius = 1.25f;

        [Header("Dig Step")]
        [SerializeField, Range(0f, 5f)] private float minStepLength = 0.1f;
        [SerializeField, Range(0.1f, 10f)] private float maxStepLength = 1.25f;
        [SerializeField, Range(0.1f, 200f)] private float digUnitsPerMeter = 20f;

        [Header("Yield Multipliers")]
        [SerializeField, Range(0f, 3f)] private float crustYieldMultiplier = 0.8f;
        [SerializeField, Range(0f, 3f)] private float mantleYieldMultiplier = 1.1f;
        [SerializeField, Range(0f, 3f)] private float coreYieldMultiplier = 1.6f;
        [SerializeField, Range(0f, 2f)] private float oreGradeWeight = 0.5f;

        private void OnValidate()
        {
            drillRadius = math.clamp(drillRadius, 0.1f, 10f);
            minStepLength = math.clamp(minStepLength, 0f, 5f);
            maxStepLength = math.clamp(maxStepLength, 0.1f, 10f);
            if (maxStepLength < minStepLength)
            {
                maxStepLength = minStepLength;
            }

            digUnitsPerMeter = math.clamp(digUnitsPerMeter, 0.1f, 200f);
            crustYieldMultiplier = math.max(0f, crustYieldMultiplier);
            mantleYieldMultiplier = math.max(0f, mantleYieldMultiplier);
            coreYieldMultiplier = math.max(0f, coreYieldMultiplier);
            oreGradeWeight = math.max(0f, oreGradeWeight);
        }

        private sealed class Baker : Baker<Space4XMiningDigConfigAuthoring>
        {
            public override void Bake(Space4XMiningDigConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Space4XMiningDigConfig
                {
                    DrillRadius = math.max(0.1f, authoring.drillRadius),
                    MinStepLength = math.max(0f, authoring.minStepLength),
                    MaxStepLength = math.max(authoring.minStepLength, authoring.maxStepLength),
                    DigUnitsPerMeter = math.max(0.1f, authoring.digUnitsPerMeter),
                    CrustYieldMultiplier = math.max(0f, authoring.crustYieldMultiplier),
                    MantleYieldMultiplier = math.max(0f, authoring.mantleYieldMultiplier),
                    CoreYieldMultiplier = math.max(0f, authoring.coreYieldMultiplier),
                    OreGradeWeight = math.max(0f, authoring.oreGradeWeight)
                });
            }
        }
    }
}
