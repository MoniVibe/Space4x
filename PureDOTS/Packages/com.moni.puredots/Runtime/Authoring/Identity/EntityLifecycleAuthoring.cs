using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Identity;

namespace PureDOTS.Authoring.Identity
{
    [DisallowMultipleComponent]
    public sealed class EntityLifecycleAuthoring : MonoBehaviour
    {
        [SerializeField] private EntityLifecycleStatus initialStatus = EntityLifecycleStatus.Alive;
        [SerializeField] private string reason;

        private sealed class Baker : Baker<EntityLifecycleAuthoring>
        {
            public override void Bake(EntityLifecycleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EntityLifecycle
                {
                    Status = authoring.initialStatus,
                    SpawnTick = 0,
                    LastChangeTick = 0,
                    Reason = new FixedString64Bytes(authoring.reason ?? string.Empty)
                });
            }
        }
    }
}

