using PureDOTS.Runtime.Movement;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for full 6DoF space movement.
    /// Adds SpaceMovementTag to entities.
    /// Use this on Space4X ships, carriers, projectiles, etc.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpaceMovementAuthoring : MonoBehaviour
    {
        // Currently no config needed for space movement (full 6DoF, no constraints)
        // Future: Could add config for max angular velocity, thruster limits, etc.
    }

    public sealed class SpaceMovementBaker : Baker<SpaceMovementAuthoring>
    {
        public override void Bake(SpaceMovementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add movement tag
            AddComponent(entity, new SpaceMovementTag());

            // Future: Add SpaceMovementConfig if we add one later
        }
    }
}

