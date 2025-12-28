using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Presentation
{
    public struct Space4XResourceMarkerPresentationConfig : IComponentData
    {
        public float BaseScale;
        public float MaxScale;
        public float OffsetMultiplier;
        public float Smoothing;
        public float BoundsExtents;
        public float DepletedThreshold;
        public byte UseCameraHemisphere;

        public static Space4XResourceMarkerPresentationConfig Default => new Space4XResourceMarkerPresentationConfig
        {
            BaseScale = 0.6f,
            MaxScale = 1.2f,
            OffsetMultiplier = 1.05f,
            Smoothing = 8f,
            BoundsExtents = 2f,
            DepletedThreshold = 0.01f,
            UseCameraHemisphere = 1
        };
    }

    public sealed class Space4XResourceMarkerPresentationConfigAuthoring : MonoBehaviour
    {
        public float BaseScale = Space4XResourceMarkerPresentationConfig.Default.BaseScale;
        public float MaxScale = Space4XResourceMarkerPresentationConfig.Default.MaxScale;
        public float OffsetMultiplier = Space4XResourceMarkerPresentationConfig.Default.OffsetMultiplier;
        public float Smoothing = Space4XResourceMarkerPresentationConfig.Default.Smoothing;
        public float BoundsExtents = Space4XResourceMarkerPresentationConfig.Default.BoundsExtents;
        public float DepletedThreshold = Space4XResourceMarkerPresentationConfig.Default.DepletedThreshold;
        public bool UseCameraHemisphere = true;
    }

    public sealed class Space4XResourceMarkerPresentationConfigBaker : Baker<Space4XResourceMarkerPresentationConfigAuthoring>
    {
        public override void Bake(Space4XResourceMarkerPresentationConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Space4XResourceMarkerPresentationConfig
            {
                BaseScale = math.max(0.001f, authoring.BaseScale),
                MaxScale = math.max(0.001f, authoring.MaxScale),
                OffsetMultiplier = math.max(0f, authoring.OffsetMultiplier),
                Smoothing = math.max(0f, authoring.Smoothing),
                BoundsExtents = math.max(0.01f, authoring.BoundsExtents),
                DepletedThreshold = math.max(0f, authoring.DepletedThreshold),
                UseCameraHemisphere = authoring.UseCameraHemisphere ? (byte)1 : (byte)0
            });
        }
    }
}
