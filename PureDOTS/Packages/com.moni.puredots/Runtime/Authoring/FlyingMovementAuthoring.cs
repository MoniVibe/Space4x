using PureDOTS.Runtime.Movement;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for flying/hovering movement (2.5D).
    /// Adds FlyingMovementTag and FlyingMovementConfig to entities.
    /// Use this on flying creatures, drones, aircraft, etc.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlyingMovementAuthoring : MonoBehaviour
    {
        [Header("Altitude Constraints")]
        [Tooltip("Minimum altitude above terrain.")]
        [Range(0f, 100f)]
        public float minAltitude = 5f;

        [Tooltip("Maximum altitude above terrain.")]
        [Range(1f, 500f)]
        public float maxAltitude = 50f;

        [Tooltip("Preferred cruising altitude above terrain.")]
        [Range(1f, 500f)]
        public float preferredAltitude = 20f;

        [Header("Movement")]
        [Tooltip("Rate of altitude change (units per second).")]
        [Range(0.1f, 50f)]
        public float altitudeChangeRate = 5f;
    }

    public sealed class FlyingMovementBaker : Baker<FlyingMovementAuthoring>
    {
        public override void Bake(FlyingMovementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add movement tag
            AddComponent(entity, new FlyingMovementTag());

            // Add movement config
            AddComponent(entity, new FlyingMovementConfig
            {
                MinAltitude = math.max(0f, authoring.minAltitude),
                MaxAltitude = math.max(authoring.minAltitude, authoring.maxAltitude),
                PreferredAltitude = math.clamp(authoring.preferredAltitude, authoring.minAltitude, authoring.maxAltitude),
                AltitudeChangeRate = math.max(0.1f, authoring.altitudeChangeRate)
            });
        }
    }
}

