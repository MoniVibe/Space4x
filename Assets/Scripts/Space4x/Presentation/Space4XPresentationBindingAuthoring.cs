using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// Simple authoring hook so designers can assign presentation descriptors per entity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XPresentationBindingAuthoring : MonoBehaviour
    {
        [Header("Descriptor")]
        [Tooltip("Descriptor key defined in the PresentationRegistry asset (e.g. 'space4x.vessel.miner').")]
        public string descriptorKey = "space4x.placeholder";

        [Header("Overrides")]
        public Vector3 positionOffset;
        public Vector3 rotationOffsetEuler;
        [Min(0.01f)]
        public float scaleMultiplier = 1f;
        public bool overrideTransform;
        public bool overrideScale;
        public bool overrideTint;
        public Color tint = Color.white;
        [Tooltip("Optional seed so pooled visuals can randomize materials/animation variants per entity.")]
        public uint variantSeed;

        private void OnValidate()
        {
            scaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
            descriptorKey = string.IsNullOrWhiteSpace(descriptorKey)
                ? "space4x.placeholder"
                : descriptorKey.Trim();
        }

        public sealed class Baker : Unity.Entities.Baker<Space4XPresentationBindingAuthoring>
        {
            public override void Bake(Space4XPresentationBindingAuthoring authoring)
            {
                if (!PresentationKeyUtility.TryParseKey(authoring.descriptorKey, out var descriptor, out _))
                {
                    Debug.LogWarning($"Space4XPresentationBindingAuthoring '{authoring.name}' has an invalid descriptor key '{authoring.descriptorKey}'.");
                    return;
                }

                bool tintOverride = authoring.overrideTint;
                bool scaleOverride = authoring.overrideScale || math.abs(authoring.scaleMultiplier - 1f) > math.EPSILON;
                bool transformOverride = authoring.overrideTransform
                                         || authoring.positionOffset != Vector3.zero
                                         || authoring.rotationOffsetEuler != Vector3.zero;

                var binding = new Space4XPresentationBinding
                {
                    Descriptor = descriptor,
                    PositionOffset = new float3(authoring.positionOffset.x, authoring.positionOffset.y, authoring.positionOffset.z),
                    RotationOffset = quaternion.EulerXYZ(math.radians(authoring.rotationOffsetEuler)),
                    ScaleMultiplier = math.max(0.01f, authoring.scaleMultiplier),
                    Tint = tintOverride
                        ? new float4(authoring.tint.r, authoring.tint.g, authoring.tint.b, authoring.tint.a)
                        : float4.zero,
                    VariantSeed = authoring.variantSeed,
                    Flags = Space4XPresentationFlagUtility.WithOverrides(tintOverride, scaleOverride, transformOverride)
                };

                var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                AddComponent(entity, binding);
            }
        }
    }
}
