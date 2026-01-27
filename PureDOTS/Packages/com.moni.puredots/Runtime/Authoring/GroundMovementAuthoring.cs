using PureDOTS.Runtime.Movement;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for ground-based movement.
    /// Adds GroundMovementTag and GroundMovementConfig to entities.
    /// Use this on villagers, ground units, animals, etc.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundMovementAuthoring : MonoBehaviour
    {
        [Header("Surface Alignment")]
        [Tooltip("Whether to align rotation to terrain surface normal (true) or keep upright (false).")]
        public bool alignToSurface = true;

        [Header("Slope Constraints")]
        [Tooltip("Maximum slope angle (in degrees) the entity can traverse.")]
        [Range(0f, 90f)]
        public float maxSlopeDegrees = 45f;

        [Header("Height Offset")]
        [Tooltip("Offset from terrain surface (for hovering slightly above ground).")]
        [Range(0f, 10f)]
        public float heightOffset = 0f;
    }

    public sealed class GroundMovementBaker : Baker<GroundMovementAuthoring>
    {
        public override void Bake(GroundMovementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add movement tag
            AddComponent(entity, new GroundMovementTag());

            // Add movement config
            AddComponent(entity, new GroundMovementConfig
            {
                AlignToSurface = authoring.alignToSurface,
                MaxSlopeAngle = math.radians(math.clamp(authoring.maxSlopeDegrees, 0f, 90f)),
                HeightOffset = math.max(0f, authoring.heightOffset)
            });
        }
    }
}

