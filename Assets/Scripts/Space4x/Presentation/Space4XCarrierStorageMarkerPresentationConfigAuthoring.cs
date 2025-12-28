using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Presentation
{
    public sealed class Space4XCarrierStorageMarkerPresentationConfigAuthoring : MonoBehaviour
    {
        public Vector3 LocalOffset = new Vector3(0f, 1.35f, 0f);
        public float BaseScale = Space4XCarrierStorageMarkerPresentationConfig.Default.BaseScale;
        public float MaxScale = Space4XCarrierStorageMarkerPresentationConfig.Default.MaxScale;
        public float Smoothing = Space4XCarrierStorageMarkerPresentationConfig.Default.Smoothing;
        public float BoundsExtents = Space4XCarrierStorageMarkerPresentationConfig.Default.BoundsExtents;
        public float DepletedThreshold = Space4XCarrierStorageMarkerPresentationConfig.Default.DepletedThreshold;
        public bool UseHysteresis = Space4XCarrierStorageMarkerPresentationConfig.Default.UseHysteresis;
        public float ShowFillThreshold = Space4XCarrierStorageMarkerPresentationConfig.Default.ShowFillThreshold;
        public float HideFillThreshold = Space4XCarrierStorageMarkerPresentationConfig.Default.HideFillThreshold;
        public float PulseDuration = Space4XCarrierStorageMarkerPresentationConfig.Default.PulseDuration;
        public float PulseIntensity = Space4XCarrierStorageMarkerPresentationConfig.Default.PulseIntensity;
    }

    public sealed class Space4XCarrierStorageMarkerPresentationConfigBaker : Baker<Space4XCarrierStorageMarkerPresentationConfigAuthoring>
    {
        public override void Bake(Space4XCarrierStorageMarkerPresentationConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Space4XCarrierStorageMarkerPresentationConfig
            {
                LocalOffset = new float3(authoring.LocalOffset.x, authoring.LocalOffset.y, authoring.LocalOffset.z),
                BaseScale = math.max(0.001f, authoring.BaseScale),
                MaxScale = math.max(0.001f, authoring.MaxScale),
                Smoothing = math.max(0f, authoring.Smoothing),
                BoundsExtents = math.max(0.01f, authoring.BoundsExtents),
                DepletedThreshold = math.max(0f, authoring.DepletedThreshold),
                UseHysteresis = authoring.UseHysteresis,
                ShowFillThreshold = math.max(0f, authoring.ShowFillThreshold),
                HideFillThreshold = math.max(0f, authoring.HideFillThreshold),
                PulseDuration = math.max(0f, authoring.PulseDuration),
                PulseIntensity = math.max(0f, authoring.PulseIntensity)
            });
        }
    }
}
