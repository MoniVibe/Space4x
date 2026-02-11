using Space4X.Runtime;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Movement Inertia Config")]
    public sealed class Space4XMovementInertiaConfigAuthoring : MonoBehaviour
    {
        [Header("Inertial Movement")]
        [SerializeField] private bool inertialMovementV1 = true;
        [SerializeField, Min(0)] private int throttleRampTicks = 12;

        [Header("Gravity")]
        [SerializeField] private bool gravityEnabled = true;
        [SerializeField, Min(0f)] private float gravityQueryRadius = 800f;
        [SerializeField, Min(0f)] private float gravityScale = 1f;
        [SerializeField, Min(0.01f)] private float gravityMinDistance = 1f;
        [SerializeField, Min(0)] private int gravityMaxSources = 4;
        [SerializeField, Min(0)] private int gravityMaxCellRadius = 4;
        [SerializeField, Min(0f)] private float gravityMaxAccel = 0f;

        private void OnValidate()
        {
            throttleRampTicks = Mathf.Max(0, throttleRampTicks);
            gravityQueryRadius = Mathf.Max(0f, gravityQueryRadius);
            gravityScale = Mathf.Max(0f, gravityScale);
            gravityMinDistance = Mathf.Max(0.01f, gravityMinDistance);
            gravityMaxSources = Mathf.Max(0, gravityMaxSources);
            gravityMaxCellRadius = Mathf.Max(0, gravityMaxCellRadius);
            gravityMaxAccel = Mathf.Max(0f, gravityMaxAccel);
        }

        private sealed class Baker : Baker<Space4XMovementInertiaConfigAuthoring>
        {
            public override void Bake(Space4XMovementInertiaConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var config = Space4XMovementInertiaConfig.Default;
                config.InertialMovementV1 = authoring.inertialMovementV1 ? (byte)1 : (byte)0;
                config.ThrottleRampTicks = (ushort)Mathf.Clamp(authoring.throttleRampTicks, 0, ushort.MaxValue);
                config.GravityEnabled = authoring.gravityEnabled ? (byte)1 : (byte)0;
                config.GravityQueryRadius = Mathf.Max(0f, authoring.gravityQueryRadius);
                config.GravityScale = Mathf.Max(0f, authoring.gravityScale);
                config.GravityMinDistance = Mathf.Max(0.01f, authoring.gravityMinDistance);
                config.GravityMaxSources = (ushort)Mathf.Clamp(authoring.gravityMaxSources, 0, ushort.MaxValue);
                config.GravityMaxCellRadius = (byte)Mathf.Clamp(authoring.gravityMaxCellRadius, 0, byte.MaxValue);
                config.GravityMaxAccel = Mathf.Max(0f, authoring.gravityMaxAccel);

                AddComponent(entity, config);
            }
        }
    }
}
