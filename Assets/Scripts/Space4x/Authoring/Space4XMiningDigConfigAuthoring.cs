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

        [Header("Laser")]
        [SerializeField, Range(0.1f, 10f)] private float laserRadius = 1.8f;
        [SerializeField, Range(0.1f, 20f)] private float laserStepLength = 3f;
        [SerializeField, Range(0f, 2f)] private float laserYieldMultiplier = 0.6f;
        [SerializeField, Range(0f, 5f)] private float laserHeatDelta = 1.5f;
        [SerializeField, Range(0f, 5f)] private float laserInstabilityDelta = 0.8f;

        [Header("Microwave")]
        [SerializeField, Range(0.1f, 10f)] private float microwaveRadius = 2.5f;
        [SerializeField, Range(0, 255)] private int microwaveDamageDelta = 12;
        [SerializeField, Range(0, 255)] private int microwaveDamageThreshold = 200;
        [SerializeField, Range(0f, 2f)] private float microwaveYieldMultiplier = 0.4f;
        [SerializeField, Range(0f, 5f)] private float microwaveHeatDelta = 2.5f;
        [SerializeField, Range(0f, 5f)] private float microwaveInstabilityDelta = 1.2f;

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
            laserRadius = math.clamp(laserRadius, 0.1f, 10f);
            laserStepLength = math.clamp(laserStepLength, 0.1f, 20f);
            laserYieldMultiplier = math.max(0f, laserYieldMultiplier);
            laserHeatDelta = math.max(0f, laserHeatDelta);
            laserInstabilityDelta = math.max(0f, laserInstabilityDelta);

            microwaveRadius = math.clamp(microwaveRadius, 0.1f, 10f);
            microwaveDamageDelta = math.clamp(microwaveDamageDelta, 0, 255);
            microwaveDamageThreshold = math.clamp(microwaveDamageThreshold, 0, 255);
            microwaveYieldMultiplier = math.max(0f, microwaveYieldMultiplier);
            microwaveHeatDelta = math.max(0f, microwaveHeatDelta);
            microwaveInstabilityDelta = math.max(0f, microwaveInstabilityDelta);

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
                    LaserRadius = math.max(0.1f, authoring.laserRadius),
                    LaserStepLength = math.max(0.1f, authoring.laserStepLength),
                    LaserYieldMultiplier = math.max(0f, authoring.laserYieldMultiplier),
                    LaserHeatDelta = math.max(0f, authoring.laserHeatDelta),
                    LaserInstabilityDelta = math.max(0f, authoring.laserInstabilityDelta),
                    MicrowaveRadius = math.max(0.1f, authoring.microwaveRadius),
                    MicrowaveDamageDelta = (byte)math.clamp(authoring.microwaveDamageDelta, 0, 255),
                    MicrowaveDamageThreshold = (byte)math.clamp(authoring.microwaveDamageThreshold, 0, 255),
                    MicrowaveYieldMultiplier = math.max(0f, authoring.microwaveYieldMultiplier),
                    MicrowaveHeatDelta = math.max(0f, authoring.microwaveHeatDelta),
                    MicrowaveInstabilityDelta = math.max(0f, authoring.microwaveInstabilityDelta),
                    CrustYieldMultiplier = math.max(0f, authoring.crustYieldMultiplier),
                    MantleYieldMultiplier = math.max(0f, authoring.mantleYieldMultiplier),
                    CoreYieldMultiplier = math.max(0f, authoring.coreYieldMultiplier),
                    OreGradeWeight = math.max(0f, authoring.oreGradeWeight)
                });
            }
        }
    }
}
