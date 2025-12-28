using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Strike Craft Dogfight Config")]
    public sealed class Space4XStrikeCraftDogfightConfigAuthoring : MonoBehaviour
    {
        [Header("Targeting")]
        [Min(0f)] public float acquireRadius = 200f;
        [Range(1f, 180f)] public float coneDegrees = 45f;

        [Header("Guidance")]
        [Min(0.1f)] public float navConstantN = 3.5f;
        [Min(0f)] public float jinkStrength = 0.15f;

        [Header("Breakoff/Rejoin")]
        [Min(0f)] public float breakOffDistance = 20f;
        [Min(0)] public int breakOffTicks = 90;
        [Min(0f)] public float rejoinRadius = 6f;
        public Vector3 rejoinOffset = new Vector3(0f, 0f, -12f);

        public sealed class Baker : Baker<Space4XStrikeCraftDogfightConfigAuthoring>
        {
            public override void Bake(Space4XStrikeCraftDogfightConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var config = StrikeCraftDogfightConfig.Default;
                config.TargetAcquireRadius = Mathf.Max(0f, authoring.acquireRadius);
                config.FireConeDegrees = Mathf.Clamp(authoring.coneDegrees, 1f, 180f);
                config.NavConstantN = Mathf.Max(0.1f, authoring.navConstantN);
                config.BreakOffDistance = Mathf.Max(0f, authoring.breakOffDistance);
                config.BreakOffTicks = (uint)Mathf.Max(0, authoring.breakOffTicks);
                config.RejoinRadius = Mathf.Max(0f, authoring.rejoinRadius);
                config.RejoinOffset = new float3(authoring.rejoinOffset.x, authoring.rejoinOffset.y, authoring.rejoinOffset.z);
                config.JinkStrength = Mathf.Max(0f, authoring.jinkStrength);
                AddComponent(entity, config);
            }
        }
    }
}
