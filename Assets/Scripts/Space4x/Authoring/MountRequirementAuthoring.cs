using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that specifies the mount requirements for a module.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Mount Requirement")]
    public sealed class MountRequirementAuthoring : MonoBehaviour
    {
        [Tooltip("Required mount type for this module")]
        public MountType mountType = MountType.Core;

        [Tooltip("Required mount size for this module")]
        public MountSize mountSize = MountSize.S;

        public sealed class Baker : Unity.Entities.Baker<MountRequirementAuthoring>
        {
            public override void Bake(MountRequirementAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.MountRequirement
                {
                    Type = authoring.mountType,
                    Size = authoring.mountSize
                });
            }
        }
    }
}

