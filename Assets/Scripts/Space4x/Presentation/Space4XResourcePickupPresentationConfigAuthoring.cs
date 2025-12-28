using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Presentation
{
    public sealed class Space4XResourcePickupPresentationConfigAuthoring : MonoBehaviour
    {
        public float Lift = Space4XResourcePickupPresentationConfig.Default.Lift;
        public float MinScale = Space4XResourcePickupPresentationConfig.Default.MinScale;
        public float MaxScale = Space4XResourcePickupPresentationConfig.Default.MaxScale;
        public float AmountForMaxScale = Space4XResourcePickupPresentationConfig.Default.AmountForMaxScale;
        public float BoundsExtents = Space4XResourcePickupPresentationConfig.Default.BoundsExtents;
        public float Smoothing = Space4XResourcePickupPresentationConfig.Default.Smoothing;
        public bool UseSourceEntityAlignment = Space4XResourcePickupPresentationConfig.Default.UseSourceEntityAlignment;
        public Vector3 LocalOffsetWhenAligned = new Vector3(0f, 0.35f, 0f);
    }

    public sealed class Space4XResourcePickupPresentationConfigBaker : Baker<Space4XResourcePickupPresentationConfigAuthoring>
    {
        public override void Bake(Space4XResourcePickupPresentationConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Space4XResourcePickupPresentationConfig
            {
                Lift = math.max(0f, authoring.Lift),
                MinScale = math.max(0.001f, authoring.MinScale),
                MaxScale = math.max(0.001f, authoring.MaxScale),
                AmountForMaxScale = math.max(0.001f, authoring.AmountForMaxScale),
                BoundsExtents = math.max(0.01f, authoring.BoundsExtents),
                Smoothing = math.max(0f, authoring.Smoothing),
                UseSourceEntityAlignment = authoring.UseSourceEntityAlignment,
                LocalOffsetWhenAligned = new float3(authoring.LocalOffsetWhenAligned.x, authoring.LocalOffsetWhenAligned.y, authoring.LocalOffsetWhenAligned.z)
            });
        }
    }
}
