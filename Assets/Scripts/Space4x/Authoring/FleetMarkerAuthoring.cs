using Space4X.Presentation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    public class FleetMarkerAuthoring : MonoBehaviour
    {
        public float Size = 1f;
        public int MeshIndex = 0;

        public class Baker : Baker<FleetMarkerAuthoring>
        {
            public override void Bake(FleetMarkerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

                AddComponent<FleetImpostorTag>(entity);
                
                AddComponent(entity, new FleetAggregateData
                {
                    Centroid = float3.zero,
                    Strength = 0f,
                    ShipCount = 0,
                    FactionId = 0
                });

                AddComponent(entity, new FleetIconMesh
                {
                    MeshIndex = authoring.MeshIndex,
                    Size = authoring.Size
                });

                AddComponent(entity, new FleetVolumeBubble
                {
                    Radius = 10f,
                    Color = new float4(0, 1, 0, 0.2f)
                });

                AddComponent(entity, new FleetStrengthIndicator
                {
                    NormalizedStrength = 0f,
                    IndicatorLevel = 0
                });
                
                AddComponent<ShouldRenderTag>(entity);
            }
        }
    }
}
