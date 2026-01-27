#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Spatial;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
#if GODGAME
using Godgame.Runtime;
#endif

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class RainCloudAuthoring : MonoBehaviour
    {
        [Header("Visual")]
        public PlaceholderVisualAuthoring placeholderVisual;

        [Header("Cloud Behaviour")]
        [Min(0f)] public float baseRadius = 6f;
        [Min(0f)] public float minRadius = 3f;
        public float radiusPerHeight = 0.5f;
        [Min(0f)] public float moisturePerSecond = 5f;
        [Min(0.01f)] public float moistureFalloff = 1f;
        [Min(0f)] public float moistureCapacity = 0f;
        public Vector3 defaultVelocity = new(0.5f, 0f, 0.5f);
        [Min(0f)] public float driftNoiseStrength = 0.75f;
        [Min(0f)] public float driftNoiseFrequency = 0.25f;
        [Range(0.01f, 1f)] public float followLerp = 0.2f;

        [Header("Pickup Settings")]
        [Min(0.01f)] public float pickableMass = 10f;
        [Min(0f)] public float pickableMaxHoldDistance = 30f;
        [Min(0.1f)] public float pickableFollowLerp = 0.25f;
        [Min(0.1f)] public float pickableThrowMultiplier = 1f;
    }

    public sealed class RainCloudBaker : Baker<RainCloudAuthoring>
    {
        public override void Bake(RainCloudAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

            AddComponent<RainCloudTag>(entity);
            AddComponent<SpatialIndexedTag>(entity);
            AddComponent(entity, new RainCloudConfig
            {
                BaseRadius = authoring.baseRadius,
                MinRadius = authoring.minRadius,
                RadiusPerHeight = authoring.radiusPerHeight,
                MoisturePerSecond = authoring.moisturePerSecond,
                MoistureFalloff = math.max(0.01f, authoring.moistureFalloff),
                MoistureCapacity = authoring.moistureCapacity,
                DefaultVelocity = new float3(authoring.defaultVelocity.x, authoring.defaultVelocity.y, authoring.defaultVelocity.z),
                DriftNoiseStrength = authoring.driftNoiseStrength,
                DriftNoiseFrequency = authoring.driftNoiseFrequency,
                FollowLerp = authoring.followLerp
            });

            AddComponent(entity, new RainCloudState
            {
                MoistureRemaining = authoring.moistureCapacity,
                ActiveRadius = authoring.baseRadius,
                Velocity = new float3(authoring.defaultVelocity.x, authoring.defaultVelocity.y, authoring.defaultVelocity.z),
                AgeSeconds = 0f,
                Flags = 0
            });

#if GODGAME
            AddComponent(entity, new HandPickable
            {
                Mass = math.max(0.01f, authoring.pickableMass),
                MaxHoldDistance = authoring.pickableMaxHoldDistance,
                ThrowImpulseMultiplier = math.max(0.1f, authoring.pickableThrowMultiplier),
                FollowLerp = math.clamp(authoring.pickableFollowLerp, 0.01f, 1f)
            });
            AddComponent<HeldByPlayer>(entity);
            SetComponentEnabled<HeldByPlayer>(entity, false);
            AddComponent<MovementSuppressed>(entity);
            SetComponentEnabled<MovementSuppressed>(entity, false);
            AddComponent<BeingThrown>(entity);
            SetComponentEnabled<BeingThrown>(entity, false);
#endif

            AddBuffer<RainCloudMoistureHistory>(entity);
        }
    }
}
#endif
