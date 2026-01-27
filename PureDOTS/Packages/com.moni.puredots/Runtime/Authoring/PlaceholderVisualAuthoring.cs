using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component that tags convert-to-entity objects with lightweight placeholder visual data.
    /// Designers can assign simple meshes/materials (boxes, barrels, vegetation, miracles) and let DOTS drive scaling/pulsing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaceholderVisualAuthoring : MonoBehaviour
    {
        [Header("Visual Type")]
        public PlaceholderVisualKind kind = PlaceholderVisualKind.Crate;

        [Header("General Appearance")]
        [Min(0.01f)] public float baseScale = 1f;
        public Vector3 localOffset;
        public Color baseColor = new(0.78f, 0.78f, 0.78f, 1f);
        [Tooltip("When enabled, keeps the authoring transform uniformly scaled to Base Scale for clarity.")]
        public bool enforceTransformScale = true;

        [Header("Miracle Pulse")]
        [Min(0f)] public float miracleBaseIntensity = 1f;
        [Min(0f)] public float miraclePulseAmplitude = 0.35f;
        [Min(0.01f)] public float miraclePulseSpeed = 2.5f;
        public Color miracleGlowColor = new(0.6f, 0.85f, 1.2f, 1f);

        [Header("Vegetation Scaling (multipliers relative to Base Scale)")]
        [Min(0.01f)] public float seedlingScale = 0.25f;
        [Min(0.01f)] public float growingScale = 0.55f;
        [Min(0.01f)] public float matureScale = 1.0f;
        [Min(0.01f)] public float fruitingScale = 1.15f;
        [Min(0.01f)] public float dyingScale = 0.8f;
        [Min(0.01f)] public float deadScale = 0.45f;
        [Min(0f)] public float scaleLerpSeconds = 0.15f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!enforceTransformScale)
            {
                return;
            }

            var t = transform;
            if (t == null)
            {
                return;
            }

            float clamped = math.max(0.01f, baseScale);
            t.localScale = new Vector3(clamped, clamped, clamped);
        }
#endif
    }

    public sealed class PlaceholderVisualBaker : Baker<PlaceholderVisualAuthoring>
    {
        public override void Bake(PlaceholderVisualAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
            float baseScale = math.max(0.01f, authoring.baseScale);

            // Note: We don't set LocalTransform here because Physics bakers (CapsuleBaker, SphereBaker, etc.)
            // may already add it. The Physics bakers will set LocalTransform correctly based on the GameObject's transform.
            // Custom scale and offset are stored in PlaceholderVisual for rendering systems to use.

            AddComponent(entity, new PlaceholderVisual
            {
                Kind = authoring.kind,
                BaseScale = baseScale,
                LocalOffset = new float3(authoring.localOffset.x, authoring.localOffset.y, authoring.localOffset.z)
            });

            switch (authoring.kind)
            {
                case PlaceholderVisualKind.Vegetation:
                {
                    var scaleSettings = new PlaceholderVegetationScale
                    {
                        SeedlingScale = math.max(0.01f, authoring.seedlingScale) * baseScale,
                        GrowingScale = math.max(0.01f, authoring.growingScale) * baseScale,
                        MatureScale = math.max(0.01f, authoring.matureScale) * baseScale,
                        FruitingScale = math.max(0.01f, authoring.fruitingScale) * baseScale,
                        DyingScale = math.max(0.01f, authoring.dyingScale) * baseScale,
                        DeadScale = math.max(0.01f, authoring.deadScale) * baseScale,
                        LerpSeconds = authoring.scaleLerpSeconds
                    };

                    AddComponent(entity, scaleSettings);

                    var scaleState = new PlaceholderVegetationScaleState
                    {
                        CurrentScale = scaleSettings.SeedlingScale
                    };

                    AddComponent(entity, scaleState);
                    break;
                }

                case PlaceholderVisualKind.Miracle:
                {
                    var glow = (Vector4)authoring.miracleGlowColor;
                    uint seed = (uint)(authoring.GetInstanceID() * 747796405u + 2891336453u);
                    float initialPhase = (seed & 0x3FFu) / 1023f * math.PI * 2f;

                    var pulse = new MiraclePlaceholderPulse
                    {
                        BaseColor = new float4(glow.x, glow.y, glow.z, glow.w),
                        BaseIntensity = math.max(0f, authoring.miracleBaseIntensity),
                        PulseAmplitude = math.max(0f, authoring.miraclePulseAmplitude),
                        PulseSpeed = math.max(0.01f, authoring.miraclePulseSpeed),
                        Phase = initialPhase
                    };

                    AddComponent(entity, pulse);

                    var emission = new URPMaterialPropertyEmissionColor
                    {
                        Value = new float4(glow.x, glow.y, glow.z, 1f)
                    };

                    AddComponent(entity, emission);
                    AddComponent(entity, new MaterialEmissionOverride
                    {
                        Value = new float4(glow.x, glow.y, glow.z, 1f)
                    });
                    break;
                }
            }

            var tint = (Vector4)authoring.baseColor;
            AddComponent(entity, new MaterialColorOverride
            {
                Value = new float4(tint.x, tint.y, tint.z, tint.w)
            });
            var baseColor = new URPMaterialPropertyBaseColor
            {
                Value = new float4(tint.x, tint.y, tint.z, tint.w)
            };

            AddComponent(entity, baseColor);
        }
    }
}
