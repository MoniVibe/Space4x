using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Identity;

namespace PureDOTS.Authoring.Identity
{
    [DisallowMultipleComponent]
    public sealed class EntityIntentAuthoring : MonoBehaviour
    {
        [SerializeField] [Range(0, 32)] private int capacity = 8;

        private sealed class Baker : Baker<EntityIntentAuthoring>
        {
            public override void Bake(EntityIntentAuthoring authoring)
            {
                var cap = (byte)math.clamp(authoring.capacity, 0, 32);
                if (cap == 0)
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EntityIntentQueue
                {
                    Capacity = cap,
                    PendingCount = 0
                });
                AddBuffer<EntityIntent>(entity);
            }
        }
    }
}

