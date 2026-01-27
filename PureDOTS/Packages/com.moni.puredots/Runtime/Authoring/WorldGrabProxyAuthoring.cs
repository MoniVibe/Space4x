using PureDOTS.Runtime.Hand;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class WorldGrabProxyAuthoring : MonoBehaviour
    {
        [Tooltip("Target entity to move when this proxy is grabbed.")]
        public GameObject target;

        private sealed class Baker : Baker<WorldGrabProxyAuthoring>
        {
            public override void Bake(WorldGrabProxyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var targetEntity = authoring.target != null
                    ? GetEntity(authoring.target, TransformUsageFlags.Dynamic)
                    : Entity.Null;

                AddComponent(entity, new WorldGrabProxy { Target = targetEntity });
                AddComponent<WorldManipulableTag>(entity);
            }
        }
    }
}
