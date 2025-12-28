using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Presentation
{
    public struct Space4XCargoPresentationConfig : IComponentData
    {
        public float3 LocalOffset;
        public float BaseScale;
        public float MaxScale;
        public float Smoothing;
        public float BoundsExtents;

        public static Space4XCargoPresentationConfig Default => new Space4XCargoPresentationConfig
        {
            LocalOffset = new float3(0f, -0.5f, -0.9f),
            BaseScale = 0.6f,
            MaxScale = 1.0f,
            Smoothing = 8f,
            BoundsExtents = 0.75f
        };
    }

    public sealed class Space4XCargoPresentationConfigAuthoring : MonoBehaviour
    {
        public Vector3 LocalOffset = new Vector3(0f, -0.5f, -0.9f);
        public float BaseScale = Space4XCargoPresentationConfig.Default.BaseScale;
        public float MaxScale = Space4XCargoPresentationConfig.Default.MaxScale;
        public float Smoothing = Space4XCargoPresentationConfig.Default.Smoothing;
        public float BoundsExtents = Space4XCargoPresentationConfig.Default.BoundsExtents;
    }

    public sealed class Space4XCargoPresentationConfigBaker : Baker<Space4XCargoPresentationConfigAuthoring>
    {
        public override void Bake(Space4XCargoPresentationConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Space4XCargoPresentationConfig
            {
                LocalOffset = new float3(authoring.LocalOffset.x, authoring.LocalOffset.y, authoring.LocalOffset.z),
                BaseScale = math.max(0.001f, authoring.BaseScale),
                MaxScale = math.max(0.001f, authoring.MaxScale),
                Smoothing = math.max(0f, authoring.Smoothing),
                BoundsExtents = math.max(0.01f, authoring.BoundsExtents)
            });
        }
    }
}
