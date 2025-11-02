#if UNITY_EDITOR
using PureDOTS.Presentation.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class CloudPlaceholderAuthoring : MonoBehaviour
    {
        [Min(0f)] public float altitudeOffset = 8f;
        [Min(1f)] public float uniformScale = 1f;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.9f, 0.95f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + new Vector3(0f, altitudeOffset, 0f), uniformScale * 2f);
        }
#endif
    }

    public sealed class CloudPlaceholderBaker : Baker<CloudPlaceholderAuthoring>
    {
        public override void Bake(CloudPlaceholderAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            var request = PresentationRequest.Create(PresentationPrototype.Cloud, math.max(0.1f, authoring.uniformScale));
            AddComponent(entity, request);
            AddComponent(entity, LocalTransform.FromPositionRotationScale(
                authoring.transform.position + new float3(0f, authoring.altitudeOffset, 0f),
                quaternion.identity,
                1f));
        }
    }

    [DisallowMultipleComponent]
    public sealed class VegetationPlaceholderAuthoring : MonoBehaviour
    {
        [Min(0f)] public float initialGrowth = 0.75f;
        [Min(0f)] public float maxGrowthScale = 1.5f;
    }

    public sealed class VegetationPlaceholderBaker : Baker<VegetationPlaceholderAuthoring>
    {
        public override void Bake(VegetationPlaceholderAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            float growth = math.saturate(authoring.initialGrowth);
            var request = PresentationRequest.Create(PresentationPrototype.Vegetation);
            request.Flags |= PresentationRequestFlags.HasScaleOverride;
            request.UniformScale = math.lerp(0.2f, authoring.maxGrowthScale, growth);
            AddComponent(entity, request);
            AddComponent(entity, new VegetationPresentationState
            {
                NormalizedGrowth = growth
            });
        }
    }
}
#endif
